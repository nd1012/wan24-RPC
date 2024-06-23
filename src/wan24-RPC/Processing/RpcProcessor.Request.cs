using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Messages;

/*
 * The processing of a request is usually:
 * 
 * 1. Call*Async stores a pending request and enqueues the context object
 * 2. The request queue ensures valid request parameters
 * 3. The request queue sends the message to the peer and waits for the response
 * 4. The request queue finalizes the return value and sets it (or an error) to the context object
 * 5. The Call*Async method removes and disposes the context object
 * 6. The Call*Async method returns the result (or throws an exception on error)
 * 
 * The requesting code is required to dispose any disposable return value after use. Asynchronous streamed return values (like streams) will be disconnected automatic, if 
 * the connection to the peer got lost (any attempt to access the ressource in that state will throw).
 */

namespace wan24.RPC.Processing
{
    // Request
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC request queue
        /// </summary>
        protected readonly RequestQueue Requests;
        /// <summary>
        /// Pending RPC requests
        /// </summary>
        protected readonly ConcurrentDictionary<long, Request> PendingRequests = [];

        /// <summary>
        /// Add a pending request
        /// </summary>
        /// <param name="request">Request</param>
        /// <returns>If added</returns>
        protected virtual bool AddPendingRequest(in Request request)
        {
            EnsureUndisposed();
            return PendingRequests.TryAdd(request.Id, request);
        }

        /// <summary>
        /// Get a pending request
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Request</returns>
        protected virtual Request? GetPendingRequest(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            return PendingRequests.TryGetValue(id, out Request? res) ? res : null;
        }

        /// <summary>
        /// Remove a pending request
        /// </summary>
        /// <param name="request">Request</param>
        protected virtual void RemovePendingRequest(in Request request)
        {
            EnsureUndisposed(allowDisposing: true);
            PendingRequests.TryRemove(request.Id, out _);
        }

        /// <summary>
        /// Remove a pending request
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Request</returns>
        protected virtual Request? RemovePendingRequest(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            return PendingRequests.TryRemove(id, out Request? res) ? res : null;
        }

        /// <summary>
        /// Handle a RPC response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleResponseAsync(ResponseMessage message)
        {
            Logger?.Log(LogLevel.Debug, "{this} handling response for request #{id}", ToString(), message.Id);
            // Handle disposing or request not found
            if (IsDisposing || GetPendingRequest(message.Id!.Value) is not Request request)
            {
                Logger?.Log(LogLevel.Warning, "{this} can't handle response for request #{id} (is disposing ({disposing}) or pending request not found)", ToString(), message.Id, IsDisposing);
                await HandleInvalidResponseReturnValueAsync(message).DynamicContext();
                await message.DisposeReturnValueAsync().DynamicContext();
                return;
            }
            // Deserialize the return value
            if (request.ExpectedReturnType is not null)
                await message.DeserializeReturnValueAsync(request.ExpectedReturnType, request.Cancellation).DynamicContext();
            // Complete the processing, if required
            if (request.Message is not RequestMessage requestMessage || requestMessage.WantsReturnValue)
            {
                request.ProcessorCompletion.TrySetResult(message.ReturnValue);
                return;
            }
            // Finalize the response handling
            if (message.ReturnValue is not null)
            {
                // Invalid return value (not requested)
                await HandleInvalidResponseReturnValueAsync(message).DynamicContext();
                await message.DisposeReturnValueAsync().DynamicContext();
                request.ProcessorCompletion.TrySetException(new InvalidDataException($"Request #{request.Id} didn't want a return value, but a return value of type {message.ReturnValue.GetType()} was responded"));
            }
            else
            {
                // Ensure the processing was completed
                request.ProcessorCompletion.TrySetResult(result: null);
            }
        }

        /// <summary>
        /// Handle a RPC error response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual Task HandleErrorResponseAsync(ErrorResponseMessage message)
        {
            Logger?.Log(LogLevel.Debug, "{this} handling error response for request #{id}", ToString(), message.Id);
            if (!IsDisposing && GetPendingRequest(message.Id!.Value) is Request request)
                request.ProcessorCompletion.TrySetException(message.Error);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Cancel a RPC request execution at the peer
        /// </summary>
        /// <param name="request">RPC request message</param>
        protected virtual async Task CancelRequestAsync(RequestMessage request)
        {
            Logger?.Log(LogLevel.Debug, "{this} canceling request #{id} at the peer", ToString(), request.Id);
            if (IsDisposing)
                return;
            try
            {
                await SendMessageAsync(new CancelMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = request.Id
                }, Options.Priorities.Rpc).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Debug, "{this} failed canceling request #{id}: {ex}", ToString(), request.Id, ex);
            }
        }
    }
}
