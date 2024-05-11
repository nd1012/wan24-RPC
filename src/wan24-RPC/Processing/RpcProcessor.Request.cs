using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Values;

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
            => PendingRequests.TryAdd(request.Message.Id ?? throw new ArgumentException("Missing message ID", nameof(request)), request);

        /// <summary>
        /// Get a pending request
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Request</returns>
        protected virtual Request? GetPendingRequest(in long id)
            => PendingRequests.TryGetValue(id, out Request? res) ? res : null;

        /// <summary>
        /// Remove a pending request
        /// </summary>
        /// <param name="request">Request</param>
        protected virtual void RemovePendingRequest(in Request request)
            => PendingRequests.TryRemove(request.Message.Id ?? throw new ArgumentException("Missing message ID", nameof(request)), out _);

        /// <summary>
        /// Remove a pending request
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Request</returns>
        protected virtual Request? RemovePendingRequest(in long id)
            => PendingRequests.TryRemove(id, out Request? res) ? res : null;

        /// <summary>
        /// Handle a RPC response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleResponseAsync(ResponseMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handling response for request #{id}", ToString(), message.Id);
            // Handle disposing or request not found
            if (!EnsureUndisposed(throwException: false) || GetPendingRequest(message.Id!.Value) is not Request request)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} can't handle response for request #{id} (is disposing ({disposing}) or pending request not found)", ToString(), message.Id, IsDisposing);
                await message.DisposeReturnValueAsync().DynamicContext();
                return;
            }
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
                await HandleInvalidReturnValueAsync(message).DynamicContext();
                await message.DisposeReturnValueAsync().DynamicContext();
                request.ProcessorCompletion.TrySetException(new InvalidDataException($"Request #{request.Message.Id} didn't want a return value, but a return value of type {message.ReturnValue.GetType()} was responded"));
            }
            else
            {
                // Ensure the processing was completed
                request.ProcessorCompletion.TrySetResult(result: null);
            }
        }

        /// <summary>
        /// Handle an invalid return value (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleInvalidReturnValueAsync(ResponseMessage message)
        {
            // Handle invalid stream return value (close the remote stream)
            if (message.ReturnValue is RpcStreamValue incomingStream && incomingStream.Stream.HasValue)
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} closing invalid remote stream return value for request #{id}", ToString(), message.Id);
                    await SendMessageAsync(new RemoteStreamCloseMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = incomingStream.Stream
                    }, RPC_PRIORTY).DynamicContext();
                }
                catch (ObjectDisposedException) when (IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Options.Logger?.Log(LogLevel.Warning, "{this} failed to close invalid remote stream returned for request #{id}: {ex}", ToString(), message.Id, ex);
                }
        }

        /// <summary>
        /// Handle a RPC error response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual Task HandleErrorAsync(ErrorResponseMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handling error response for request #{id}", ToString(), message.Id);
            if (EnsureUndisposed(throwException: false) && PendingRequests.TryGetValue(message.Id!.Value, out Request? request))
                request.ProcessorCompletion.TrySetException(message.Error);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Cancel a RPC request execution at the peer
        /// </summary>
        /// <param name="request">RPC request message</param>
        protected virtual async Task CancelRequestAsync(RequestMessage request)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} canceling request #{id} at the peer", ToString(), request.Id);
            if (!EnsureUndisposed(throwException: false))
                return;
            try
            {
                await SendMessageAsync(new CancellationMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = request.Id
                }).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} failed canceling request #{id}: {ex}", ToString(), request.Id, ex);
            }
        }

        /// <summary>
        /// RPC request (context)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        protected record class Request() : DisposableRecordBase()
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public required RpcProcessor Processor { get; init; }

            /// <summary>
            /// Created time
            /// </summary>
            public DateTime Created { get; } = DateTime.Now;

            /// <summary>
            /// Done time
            /// </summary>
            public DateTime Done { get; protected set; } = DateTime.MinValue;

            /// <summary>
            /// Message
            /// </summary>
            public required IRpcRequest Message { get; init; }

            /// <summary>
            /// RPC processor cancellation token
            /// </summary>
            public required CancellationToken ProcessorCancellation { get; init; }

            /// <summary>
            /// Request cancellation token
            /// </summary>
            public CancellationToken RequestCancellation { get; init; }

            /// <summary>
            /// Processor completion (completes the request queue processing)
            /// </summary>
            public TaskCompletionSource<object?> ProcessorCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Request completion (completes the whole request)
            /// </summary>
            public TaskCompletionSource<object?> RequestCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// If the request was processed by the peer (the request message was sent)
            /// </summary>
            public bool Processed { get; set; }

            /// <summary>
            /// Outgoing streams (will be disposed)
            /// </summary>
            public HashSet<OutgoingStream> Streams { get; } = [];

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (Done != DateTime.MinValue)
                    return;
                Done = DateTime.Now;
                Processor.Options.Logger?.Log(LogLevel.Trace, "{processor} RPC request #{id} processing done within {runtime}", Processor.ToString(), Message.Id, Done - Created);
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (Streams.Count > 0)
                {
                    using (SemaphoreSyncContext ssc = Processor.OutgoingStreamsSync)
                        foreach (OutgoingStream stream in Streams)
                            Processor.RemoveOutgoingStream(stream);
                    Streams.DisposeAll();
                }
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (Streams.Count > 0)
                {
                    using (SemaphoreSyncContext ssc = await Processor.OutgoingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (OutgoingStream stream in Streams)
                            Processor.RemoveOutgoingStream(stream);
                    await Streams.DisposeAllAsync().DynamicContext();
                }
            }
        }
    }
}
