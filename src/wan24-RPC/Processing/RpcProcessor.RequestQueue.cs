using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

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
                bool didReturn = false;
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
                    didReturn = true;
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
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                }
                catch (ObjectDisposedException ex) when (IsDisposing)
                {
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                }
                catch (OperationCanceledException ex) when (CancelToken.IsCancellationRequested)
                {
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken.IsEqualTo(item.Cancellation))
                {
                    if (returnValue is not null)
                    {
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                    else
                    {
                        if (item.WasProcessing && !didReturn)
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
                if (value is not null)
                {
                    // Scopeable parameter handling
                    if (value is RpcScopeValue)
                        // Nothing to do - just use the given scope value
                        return value;
                    if (value is RpcScopeBase scope)
                    {
                        // Use the given scopes value
                        Logger?.Log(LogLevel.Trace, "{this} using given scope ({scope}) value for parameter #{param}", ToString(), scope, index);
                        item.ParameterScopes.Add(scope);
                        if (scope.ScopeParameter is null)
                            await scope.CreateScopeParameterAsync(cancellationToken: cancellationToken).DynamicContext();
                        return scope.ScopeParameter.Value ?? await scope.ScopeParameter.CreateValueAsync(Processor, scope.Id, cancellationToken)
                            .DynamicContext();
                    }
                    if (value is IRpcScopeParameter scopeParameter)
                        // Create a new scope from the given scope parameter and use its scope value
                        if (RpcScopes.GetLocalScopeFactory(scopeParameter.Type) is RpcScopes.ScopeFactory_Delegate scopeFactory)
                        {
                            Logger?.Log(LogLevel.Trace, "{this} creating a new local scope type #{type} from parameter #{index} {param}", ToString(), scopeParameter.Type, index, scopeParameter.GetType());
                            RpcScopeBase scope2 = await scopeFactory.Invoke(Processor, scopeParameter, cancellationToken).DynamicContext();
                            Logger?.Log(LogLevel.Trace, "{this} created a new local scope ({scope}) from parameter #{index} {param}", ToString(), scope2, index, scopeParameter.GetType());
                            item.ParameterScopes.Add(scope2);
                            if (scope2.ScopeParameter is null)
                                await scope2.CreateScopeParameterAsync(cancellationToken: cancellationToken).DynamicContext();
                            return scope2.ScopeParameter.Value ?? await scope2.ScopeParameter.CreateValueAsync(Processor, scope2.Id, cancellationToken)
                                .DynamicContext();
                        }
                        else
                        {
                            Logger?.Log(LogLevel.Trace, "{this} can't create new local scope type #{type} from parameter #{index} {param} (scope wasn't registered)", ToString(), scopeParameter.Type, index, scopeParameter.GetType());
                            await value.TryDisposeAsync().DynamicContext();
                            throw new ArgumentException($"Failed to get scope type #{scopeParameter.Type} factory for parameter #{index}");
                        }
                    if (RpcScopes.GetParameterScopeFactory(value.GetType()) is RpcScopes.ParameterScopeFactory_Delegate scopeFactory2)
                    {
                        // Create a new scope from the given scopeable object and use its scope value
                        Logger?.Log(LogLevel.Trace, "{this} creating a new local scope from parameter #{index} value type {type}", ToString(), index, value.GetType());
                        RpcScopeBase? scope2 = await scopeFactory2.Invoke(Processor, value, cancellationToken).DynamicContext();
                        if (scope2 is not null)
                        {
                            Logger?.Log(LogLevel.Trace, "{this} created a new local scope ({scope}) from parameter #{index} value type {type}", ToString(), scope2, index, value.GetType());
                            item.ParameterScopes.Add(scope2);
                            if (scope2.ScopeParameter is null)
                                await scope2.CreateScopeParameterAsync(cancellationToken: cancellationToken).DynamicContext();
                            return scope2.ScopeParameter.Value ?? await scope2.ScopeParameter.CreateValueAsync(Processor, scope2.Id, cancellationToken)
                                .DynamicContext();
                        }
                        else
                        {
                            Logger?.Log(LogLevel.Trace, "{this} can't create new local scope from parameter #{index} value type {type} (no responsible scope registration found)", ToString(), index, value.GetType());
                            await value.TryDisposeAsync().DynamicContext();
                            return null;
                        }
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
                    Logger?.Log(LogLevel.Trace, "{this} creating new remote scope type #{type} from return value {value}", ToString(), scopeValue.Type, scopeValue.GetType());
                    if (RpcScopes.GetRemoteScopeFactory(scopeValue.Type) is not RpcScopes.RemoteScopeFactory_Delegate scopeFactory)
                        throw new InvalidDataException($"Failed to find remote scope type #{scopeValue.Type} return value factory");
                    RpcRemoteScopeBase remoteScope = await scopeFactory.Invoke(Processor, scopeValue, cancellationToken).DynamicContext();
                    Logger?.Log(LogLevel.Trace, "{this} created new remote scope ({scope}) from return value {value}", ToString(), remoteScope, scopeValue.GetType());
                    if (item.ExpectedReturnType is not null)
                        if (item.ExpectedReturnType.IsAssignableFrom(typeof(RpcScopeValue)))
                        {
                            item.ReturnScope = remoteScope;
                        }
                        else if (item.ExpectedReturnType.IsAssignableFrom(remoteScope.GetType()))
                        {
                            returnValue = remoteScope;
                        }
                        else
                        {
                            item.ReturnScope = remoteScope;
                            returnValue = remoteScope.Value;
                        }
                    return returnValue;
                }
                return returnValue;
            }
        }
    }
}
