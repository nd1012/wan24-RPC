using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

/*
 * The number of RPC requests is limited in two ways:
 * 
 * 1. Number of parallel executing RPC requests to respect the peers max. number of parallel executed calls plus the max. number of pending calls
 * 2. Number of pending requests to protect memory ressources
 * 
 * The RPC processor has to respect the peers limits, otherwise the peer will disconnect due to a protocol error. You should work work with timeout cancellation tokens 
 * to prevent dead locks on any RPC communication error. To combine them with another cancellation token use wan24.Core.Cancellations.
 */

namespace wan24.RPC.Processing
{
    // Request queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// Create a request queue
        /// </summary>
        /// <returns>Request queue</returns>
        protected virtual RequestQueue CreateRequestQueue() => new(this)
        {
            Name = "Outgoing RPC requests"
        };

        /// <summary>
        /// RPC request queue
        /// </summary>
        protected class RequestQueue(in RpcProcessor processor)
            : ParallelItemQueueWorkerBase<Request>(processor.Options.RequestQueue.Capacity, processor.Options.RequestQueue.Threads)
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Logger
            /// </summary>
            public virtual ILogger? Logger => Processor.Logger;

            /// <inheritdoc/>
            protected override async Task ProcessItem(Request item, CancellationToken cancellationToken)
            {
                await Task.Yield();
                Logger?.Log(LogLevel.Debug, "{this} processing request #{id}", ToString(), item.Message.Id);
                if (item.RequestCompletion.Task.IsCompleted)
                {
                    Logger?.Log(LogLevel.Debug, "{this} request #{id} is completed already", ToString(), item.Message.Id);
                    return;
                }
                if (item.ProcessorCompletion.Task.IsCompleted)
                {
                    Logger?.Log(LogLevel.Debug, "{this} request #{id} processor was completed already", ToString(), item.Message.Id);
                    if (item.ProcessorCompletion.Task.Exception is not null)
                    {
                        item.RequestCompletion.TrySetException(item.ProcessorCompletion.Task.Exception);
                    }
                    else
                    {
                        item.RequestCompletion.TrySetResult(item.ProcessorCompletion.Task.Result);
                    }
                    return;
                }
                if (item.Message is not RequestMessage request)
                {
                    item.RequestCompletion.TrySetException(new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)"));
                    return;
                }
                object? returnValue = null;
                try
                {
                    using Cancellations cancellation = new(Processor.CancelToken, item.Cancellation, cancellationToken);
                    // Finalize parameters
                    if (request.Parameters is not null)
                        for (int i = 0, len = request.Parameters.Length; i < len; i++)
                            if (request.Parameters[i] is not null)
                            {
                                Logger?.Log(LogLevel.Trace, "{this} resolving final request #{id} API \"{api}\" method \"{method}\" parameter #{index} value type {type}", ToString(), item.Id, request.Api, request.Method, i, request.Parameters[i]!.GetType().ToString());
                                request.Parameters[i] = await GetFinalParameterValueAsync(item, request, i, request.Parameters[i], cancellation).DynamicContext();
                                Logger?.Log(LogLevel.Trace, "{this} request #{id} API \"{api}\" method \"{method}\" parameter #{index} value type is now {type} after finalizing", ToString(), item.Id, request.Api, request.Method, i, request.Parameters[i]?.GetType().ToString() ?? "NULL");
                            }
                    // Send the RPC request and wait for the response
                    await Processor.SendMessageAsync(request, Processor.Options.Priorities.Rpc, cancellation).DynamicContext();
                    item.WasProcessing = true;
                    returnValue = await item.ProcessorCompletion.Task.DynamicContext();
                    // Handle the response
                    if (returnValue is not null)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} finalizing request #{id} API \"{api}\" method \"{method}\" return value type {type}", ToString(), item.Id, request.Api, request.Method, returnValue?.GetType().ToString() ?? "NULL");
                        returnValue = await GetFinalReturnValueAsync(item, request, returnValue, cancellation).DynamicContext();
                        Logger?.Log(LogLevel.Trace, "{this} request #{id} API \"{api}\" method \"{method}\" return value type is now {type} after finalizing", ToString(), item.Id, request.Api, request.Method, returnValue?.GetType().ToString() ?? "NULL");
                    }
                    if (!item.RequestCompletion.TrySetResult(returnValue))
                    {
                        Logger?.Log(LogLevel.Warning, "{this} request #{id} API \"{api}\" method \"{method}\" failed to set return value", ToString(), item.Id, request.Api, request.Method);
                        await returnValue.TryDisposeAsync().DynamicContext();//TODO How to handle a stream return value?
                    }
                }
                catch (ObjectDisposedException) when (IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                {
                }
                catch (OperationCanceledException ex) when (Equals(ex.CancellationToken, item.Cancellation))
                {
                    if (returnValue is not null)
                    {
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                    else
                    {
                        try
                        {
                            await Processor.CancelRequestAsync(request).DynamicContext();
                        }
                        catch (Exception ex2)
                        {
                            Logger?.Log(LogLevel.Error, "{this} request #{id} API \"{api}\" method \"{method}\" failed to cancel during queue processing: {ex}", ToString(), item.Id, request.Api, request.Method, ex2);
                        }
                    }
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                }
                catch (Exception ex)
                {
                    if (returnValue is not null)
                        await returnValue.TryDisposeAsync().DynamicContext();
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                }
                finally
                {
                    item.SetDone();
                }
            }

            /// <summary>
            /// Get the final parameter value
            /// </summary>
            /// <param name="item">RPC request</param>
            /// <param name="request">RPC request message</param>
            /// <param name="index">Zero based parameter index</param>
            /// <param name="value">Current parameter value</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>Parameter value to use</returns>
            protected virtual async Task<object?> GetFinalParameterValueAsync(
                Request item, 
                RequestMessage request, 
                int index,
                object? value, 
                CancellationToken cancellationToken
                )
            {
                // Scopeable parameter handling
                if(value is not null)
                    if(value is IRpcScopeParameter scopeParameter)
                    {
                        if(RpcScopes.Factories.TryGetValue(scopeParameter.Type, out RpcScopes.ScopeFactory_Delegate? scopeFactory))
                        {
                            RpcScopeBase scope = await scopeFactory.Invoke(Processor, scopeParameter, cancellationToken).DynamicContext();
                            item.ParameterScopes.Add(scope);
                            value = scope.ScopeParameter?.Value;
                        }
                        else
                        {
                            await value.TryDisposeAsync().DynamicContext();
                            throw new ArgumentException($"Failed to get scope type #{scopeParameter.Type} factory for parameter #{index}");
                        }
                    }
                    else if(RpcScopes.GetParameterScopeFactory(value.GetType()) is RpcScopes.ParameterScopeFactory_Delegate scopeFactory)
                    {
                        RpcScopeBase? scope = await scopeFactory.Invoke(Processor, value, cancellationToken).DynamicContext();
                        if(scope is not null)
                        {
                            item.ParameterScopes.Add(scope);
                            value = scope.Value;
                        }
                        else
                        {
                            await value.TryDisposeAsync().DynamicContext();
                            value = null;
                        }
                    }
                return value;
            }

            /// <summary>
            /// Get the final return value of a method call which will be sent back to the peer
            /// </summary>
            /// <param name="item">RPC request</param>
            /// <param name="request">RPC request message</param>
            /// <param name="returnValue">Return value</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>Final return value</returns>
            protected virtual async Task<object?> GetFinalReturnValueAsync(
                Request item,
                RequestMessage request,
                object? returnValue,
                CancellationToken cancellationToken
                )
            {
                // Remote scope handling
                if(returnValue is RpcScopeValue scopeValue)
                {
                    if(!RpcScopes.RemoteFactories.TryGetValue(scopeValue.Type, out RpcScopes.RemoteScopeFactory_Delegate? scopeFactory))
                        throw new InvalidDataException($"Failed to find remote scope type #{scopeValue.Type} return value factory");
                    RpcRemoteScopeBase remoteScope = await scopeFactory.Invoke(Processor, scopeValue, cancellationToken).DynamicContext();
                    item.ReturnScope = remoteScope;
                    returnValue = remoteScope.Value;
                }
                return returnValue;
            }
        }
    }
}
