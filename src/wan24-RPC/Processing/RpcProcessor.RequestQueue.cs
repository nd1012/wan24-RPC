using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
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
            : ParallelItemQueueWorkerBase<Request>(processor.Options.RequestQueueSize, processor.Options.RequestThreads)
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <inheritdoc/>
            protected override async Task ProcessItem(Request item, CancellationToken cancellationToken)
            {
                await Task.Yield();
                Processor.Options.Logger?.Log(LogLevel.Debug, "{this} processing request #{id}", ToString(), item.Message.Id);
                if (item.RequestCompletion.Task.IsCompleted)
                {
                    Processor.Options.Logger?.Log(LogLevel.Debug, "{this} request #{id} is completed already", ToString(), item.Message.Id);
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
                    using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.RequestCancellation);
                    // Finalize parameters
                    if (request.Parameters is not null)
                        for (int i = 0, len = request.Parameters.Length; i < len; i++)
                            if (request.Parameters[i] is not null)
                            {
                                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving final request #{id} API \"{api}\" method \"{method}\" parameter #{index} value type {type}", ToString(), item.Message.Id, request.Api, request.Method, i, request.Parameters[i]?.GetType().ToString() ?? "NULL");
                                request.Parameters[i] = await GetFinalParameterValueAsync(item, request, i, request.Parameters[i], cancellation).DynamicContext();
                                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} request #{id} API \"{api}\" method \"{method}\" parameter #{index} value type is now {type} after finalizing", ToString(), item.Message.Id, request.Api, request.Method, i, request.Parameters[i]?.GetType().ToString() ?? "NULL");
                            }
                    // Send the RPC request and wait for the response
                    await Processor.SendMessageAsync(request, RPC_PRIORTY, cancellation).DynamicContext();
                    item.Processed = true;
                    returnValue = await item.ProcessorCompletion.Task.DynamicContext();
                    // Handle the response
                    if (returnValue is not null)
                    {
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} finalizing request #{id} API \"{api}\" method \"{method}\" return value type {type}", ToString(), item.Message.Id, request.Api, request.Method, returnValue?.GetType().ToString() ?? "NULL");
                        returnValue = await GetFinalReturnValueAsync(item, request, returnValue, cancellation).DynamicContext();
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} request #{id} API \"{api}\" method \"{method}\" return value type is now {type} after finalizing", ToString(), item.Message.Id, request.Api, request.Method, returnValue?.GetType().ToString() ?? "NULL");
                    }
                    if (!item.RequestCompletion.TrySetResult(returnValue))
                    {
                        Processor.Options.Logger?.Log(LogLevel.Warning, "{this} request #{id} API \"{api}\" method \"{method}\" failed to set return value", ToString(), item.Message.Id, request.Api, request.Method);
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                }
                catch (ObjectDisposedException) when (IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                {
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == item.RequestCancellation)
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
                        catch(Exception ex2)
                        {
                            Processor.Options.Logger?.Log(LogLevel.Error, "{this} request #{id} API \"{api}\" method \"{method}\" failed to cancel during queue processing: {ex}", ToString(), item.Message.Id, request.Api, request.Method, ex2);
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
                // Stream parameter handling
                if (value is Stream stream)
                    value = Processor.CreateStreamParameter(stream);
                if (value is RpcStreamParameter streamParameter)
                    await using (streamParameter.DynamicContext())
                    {
                        RpcStreamValue streamValue = await Processor.CreateOutgoingStreamAsync(streamParameter, cancellationToken).DynamicContext();
                        value = streamValue;
                        item.Streams.Add((await Processor.GetOutgoingStreamAsync(streamValue.Stream!.Value, cancellationToken: cancellationToken).DynamicContext())!);
                    }
                //TODO Enumerations
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
                // Stream handling
                if (returnValue is RpcStreamValue streamValue)
                    returnValue = await Processor.CreateIncomingStreamAsync(streamValue, leaveOpen: true, cancellationToken).DynamicContext();
                //TODO Enumerations
                return returnValue;
            }
        }
    }
}
