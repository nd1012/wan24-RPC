using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages;

/*
 * The processing of a call is usually:
 * 
 * 1. The incoming message handler starts handling a request message (a call)
 * 2. A pending call context is being stored and enqueued for processing
 * 3. The call queue creates a RPC context and authorizes the API method call
 * 4. The call queue ensures valid API method parameters
 * 5. The call queue calls the API method and waits for the result
 * 6. The call queue finalizes the return value and sets it (or an error) to the context object
 * 7. The call message handler sends the response to the peer
 * 8. The call message handler removes the pending call context
 * 
 * The called API method doesn't need to dispose any parameter, unless the NoRpcDisposeAttribute has been applied to a specific RPC parameter.
 * 
 * The API methods return value will be disposed after sending it to the peer unless the NoRpcDisposeAttribute has been applied to the method.
 */

namespace wan24.RPC.Processing
{
    // RPC call processing
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC calls
        /// </summary>
        protected readonly CallQueue Calls;
        /// <summary>
        /// Pending calls
        /// </summary>
        protected readonly ConcurrentDictionary<long, Call> PendingCalls = [];

        /// <summary>
        /// Add a pending call
        /// </summary>
        /// <param name="call">Call</param>
        /// <returns>If added</returns>
        protected virtual bool AddPendingCall(in Call call)
        {
            EnsureUndisposed();
            return PendingCalls.TryAdd(call.Id, call);
        }

        /// <summary>
        /// Get a pending call
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Call</returns>
        protected virtual Call? GetPendingCall(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            return PendingCalls.TryGetValue(id, out Call? res) ? res : null;
        }

        /// <summary>
        /// Remove a pending call
        /// </summary>
        /// <param name="call">Call</param>
        protected virtual void RemovePendingCall(in Call call)
        {
            EnsureUndisposed(allowDisposing: true);
            PendingCalls.TryRemove(call.Id, out _);
        }

        /// <summary>
        /// Remove a pending call
        /// </summary>
        /// <param name="id">ID</param>
        protected virtual Call? RemovePendingCall(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            return PendingCalls.TryRemove(id, out Call? res) ? res : null;
        }

        /// <summary>
        /// Create a context for processing a RPC call
        /// </summary>
        /// <param name="request">Message</param>
        /// <param name="method">API method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Context</returns>
        protected virtual RpcContext CreateCallContext(
            in RequestMessage request,
            in RpcApiMethodInfo method,
            in CancellationToken cancellationToken
            )
            => Options.DefaultContext is null
                ? new()
                {
                    Created = DateTime.Now,
                    Processor = this,
                    Cancellation = cancellationToken,
                    Message = request,
                    Method = method,
                    Services = Options.DefaultServices is null
                        ? new()
                        : new(Options.DefaultServices)
                        {
                            DisposeServiceProvider = false
                        }
                }
                : Options.DefaultContext with
                {
                    Created = DateTime.Now,
                    Processor = this,
                    Cancellation = cancellationToken,
                    Message = request,
                    Method = method,
                    Services = Options.DefaultServices is null
                        ? new()
                        : new(Options.DefaultServices)
                        {
                            DisposeServiceProvider = false
                        }
                };

        /// <summary>
        /// Handle a RPC request (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleRequestAsync(RequestMessage message)
        {
            Logger?.Log(LogLevel.Debug, "{this} handle call #{id}", ToString(), message.Id);
            if (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} can't handle call when disposing", ToString());
                if (message.Parameters is not null)
                    foreach (object? parameter in message.Parameters)
                        if (parameter is not null)
                            await parameter.TryDisposeAsync().DynamicContext();
                return;
            }
            bool removePending = false,// If to remove the call from the pending calls
                processingError = false;// If the error happened during processing the call
            object? returnValue = null;// Returned value
            Exception? error = null;// Error
            using Call call = new()// Context
            {
                Processor = this,
                Message = message
            };
            try
            {
                // Store the call as pending (for handling a remote cancellation)
                if (!AddPendingCall(call))
                {
                    Logger?.Log(LogLevel.Warning, "{this} failed to add pending call #{id} (double message ID)", ToString(), message.Id);
                    Logger?.Log(LogLevel.Trace, "{this} disposing parameters of call #{id}", ToString(), message.Id);
                    error = new InvalidDataException("Double message ID");
                    return;
                }
                removePending = true;
                // Try to enqueue the call for processing
                if (!Calls.TryEnqueue(call))
                {
                    Logger?.Log(LogLevel.Warning, "{this} failed to enqueue call #{id} (too many queued calls)", ToString(), message.Id);
                    error = new TooManyRpcRequestsException("RPC call limit exceeded");
                    return;
                }
                // Wait for processing
                try
                {
                    Logger?.Log(LogLevel.Debug, "{this} wait for processing call #{id} ({count} calls queued now)", ToString(), message.Id, Calls.Count);
                    returnValue = await call.Completion.Task.DynamicContext();
                }
                catch
                {
                    processingError = true;
                    throw;
                }
                finally
                {
                    call.Context?.SetDone();
                }
                // Send the response
                Logger?.Log(LogLevel.Debug, "{this} send response for call #{id}", ToString(), message.Id);
                if (message.WantsReturnValue)
                {
                    Logger?.Log(LogLevel.Trace, "{this} send response type \"{returnValue}\" for call #{id}", ToString(), returnValue?.GetType(), message.Id);
                }
                else if (returnValue is not null)
                {
                    Logger?.Log(LogLevel.Warning, "{this} call #{id} doesn't want the return value", ToString(), message.Id);
                }
                await SendResponseAsync(message, message.WantsReturnValue ? returnValue : null).DynamicContext();
                await OnCallRespondedAsync(call, returnValue).DynamicContext();
            }
            catch (ObjectDisposedException ex) when (IsDisposing)
            {
                error = ex;
            }
            catch (OperationCanceledException ex) when (CancelToken.IsCancellationRequested)
            {
                error = ex;
            }
            catch (OperationCanceledException ex) when (call.Cancellation.IsCancellationRequested)
            {
                error = ex;
                Logger?.Log(LogLevel.Warning, "{this} call #{id} cancelled during processing", ToString(), message.Id);
            }
            catch (Exception ex)
            {
                error = ex;
                bool fatalError = !processingError || 
                    Options.DisconnectOnApiError || 
                    (call.Context?.Method?.API.DisconnectOnError ?? call.Context?.Method?.DisconnectOnError ?? false);
                Logger?.Log(!processingError ? LogLevel.Error : LogLevel.Warning, "{this} processing call #{id} failed with an exception: {ex}", ToString(), message.Id, ex);
                if (fatalError)
                    throw;
            }
            finally
            {
                if (removePending)
                    RemovePendingCall(call);
                Logger?.Log(LogLevel.Trace, "{this} finalizing call #{id} (processed: {processed})", ToString(), message.Id, call.WasProcessing);
                if (error is not null)
                {
                    if (!processingError)
                        await OnCallErrorAsync(call, error, parameters: null, returnValue).DynamicContext();
                    await SendCallErrorResponseAsync(message, error).DynamicContext();
                }
                else if (message.Parameters is not null)
                {
                    foreach (object? parameter in message.Parameters)
                        if (parameter is not null)
                            await parameter.TryDisposeAsync().DynamicContext();
                }
                call.SetDone();
                await call.DisposeAsync().DynamicContext();
            }
        }

        /// <summary>
        /// Handle a RPC request cancellation (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleCallCancellationAsync(CancelMessage message)
        {
            Logger?.Log(LogLevel.Debug, "{this} handle cancellation of call #{id}", ToString(), message.Id);
            if (!IsDisposing && GetPendingCall(message.Id!.Value) is Call call && !call.IsDisposing)
                try
                {
                    Logger?.Log(LogLevel.Debug, "{this} canceling call #{id}", ToString(), message.Id);
                    await call.Cancellation.CancelAsync().DynamicContext();
                }
                catch
                {
                }
        }

        /// <summary>
        /// Send an error response for a RPC request processing exception
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="exception">Exception</param>
        protected virtual async Task SendCallErrorResponseAsync(RequestMessage message, Exception exception)
        {
            Logger?.Log(LogLevel.Debug, "{this} sending error response {type} for call #{id}", ToString(), exception.GetType(), message.Id);
            if (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} can't send error response for call #{id} when disposing", ToString(), message.Id);
                return;
            }
            try
            {
                await SendMessageAsync(new ErrorResponseMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = message.Id,
                    Error = exception
                }, Options.Priorities.Rpc).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} failed to send error response for call #{id}: disposing", ToString(), message.Id);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} failed to send error response for call #{id}: canceled", ToString(), message.Id);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} failed to send error response for call #{id}: {ex}", ToString(), message.Id, ex);
            }
        }

        /// <summary>
        /// Send a response for a RPC request
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="returnValue">Return value (should be <see langword="null"/>, if <see cref="RequestMessage.WantsReturnValue"/> is <see langword="false"/>)</param>
        protected virtual async Task SendResponseAsync(RequestMessage message, object? returnValue)
        {
            Logger?.Log(LogLevel.Debug, "{this} sending response for call #{id}", ToString(), message.Id);
            if (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} can't send response for call #{id} when disposing", ToString(), message.Id);
                return;
            }
            try
            {
                await SendMessageAsync(new ResponseMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = message.Id,
                    ReturnValue = message.WantsReturnValue
                        ? returnValue
                        : null
                }, Options.Priorities.Rpc).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} failed to send response for call #{id}: disposing", ToString(), message.Id);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} failed to send response for call #{id}: canceled", ToString(), message.Id);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} failed to send response for call #{id}: {ex}", ToString(), message.Id, ex);
            }
        }
    }
}
