using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
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
            => PendingCalls.TryAdd(call.Message.Id ?? throw new ArgumentException("Missing message ID", nameof(call)), call);

        /// <summary>
        /// Get a pending call
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Call</returns>
        protected virtual Call? GetPendingCall(in long id)
            => PendingCalls.TryGetValue(id, out Call? res) ? res : null;

        /// <summary>
        /// Remove a pending call
        /// </summary>
        /// <param name="call">Call</param>
        protected virtual void RemovePendingCall(in Call call)
            => PendingCalls.TryRemove(call.Message.Id ?? throw new ArgumentException("Missing message ID", nameof(call)), out _);

        /// <summary>
        /// Remove a pending call
        /// </summary>
        /// <param name="id">ID</param>
        protected virtual Call? RemovePendingCall(in long id)
            => PendingCalls.TryRemove(id, out Call? res) ? res : null;

        /// <summary>
        /// Handle a RPC request (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleRequestAsync(RequestMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handle call #{id}", ToString(), message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't handle call when disposing", ToString());
                Options.Logger?.Log(LogLevel.Trace, "{this} disposing parameters of call #{id}", ToString(), message.Id);
                await message.DisposeParametersAsync().DynamicContext();
                return;
            }
            bool removePending = false,
                processingError = false;
            using Call call = new()
            {
                Processor = this,
                ProcessorCancellation = CancelToken,
                Message = message
            };
            try
            {
                // Store the call as pending (for handling a remote cancellation)
                if (!AddPendingCall(call))
                {
                    Options.Logger?.Log(LogLevel.Warning, "{this} failed to add pending call #{id} (double message ID)", ToString(), message.Id);
                    await SendErrorResponseAsync(message, new InvalidOperationException("Double message ID")).DynamicContext();
                    return;
                }
                removePending = true;
                // Try to enqueue the call for processing
                if (!Calls.TryEnqueue(call))
                {
                    Options.Logger?.Log(LogLevel.Warning, "{this} failed to enqueue call #{id} (too many queued calls)", ToString(), message.Id);
                    await SendErrorResponseAsync(message, new TooManyRpcRequestsException("RPC call limit exceeded")).DynamicContext();
                    return;
                }
                // Wait for processing
                object? returnValue;
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} wait for processing call #{id}", ToString(), message.Id);
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
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} send response for call #{id}", ToString(), message.Id);
                    if (message.WantsReturnValue)
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} send response type \"{returnValue}\" for call #{id}", ToString(), returnValue?.GetType(), message.Id);
                    }
                    else
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} doesn't want the return value", ToString(), message.Id);
                    }
                    await SendResponseAsync(message, message.WantsReturnValue ? returnValue : null).DynamicContext();
                }
                finally
                {
                    if (returnValue is not null && (call.Context is null || call.Context.Method.DisposeReturnValue))
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} disposing return value for call #{id}", ToString(), message.Id);
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                }
            }
            catch (OperationCanceledException ex) when (call.CallCancellation.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} cancelled during call #{id} processing", ToString(), message.Id);
                await SendErrorResponseAsync(message, ex).DynamicContext();
            }
            catch (Exception ex)
            {
                if (!processingError || Options.DisconnectOnApiError)
                {
                    Options.Logger?.Log(LogLevel.Error, "{this} processing call #{id} failed with an exception: {ex}", ToString(), message.Id, ex);
                    throw;
                }
                Options.Logger?.Log(LogLevel.Warning, "{this} processing call #{id} failed with an exception: {ex}", ToString(), message.Id, ex);
                await HandleCallProcessingErrorAsync(call, ex).DynamicContext();
            }
            finally
            {
                if (removePending)
                    RemovePendingCall(call);
                Options.Logger?.Log(LogLevel.Trace, "{this} disposing parameters of call #{id} (processed: {processed})", ToString(), message.Id, call.Processed);
                await message.DisposeParametersAsync(call.Processed ? call.Context?.Method : null).DynamicContext();
                call.SetDone();
            }
        }

        /// <summary>
        /// Handle a call processing error
        /// </summary>
        /// <param name="call">Call</param>
        /// <param name="exception">Exception</param>
        protected virtual Task HandleCallProcessingErrorAsync(Call call, Exception exception)
            => SendErrorResponseAsync(call.Message as RequestMessage ?? throw new ArgumentException("Request message expected", nameof(call)), exception);

        /// <summary>
        /// Handle a RPC request cancellation (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual Task HandleCancellationAsync(CancellationMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handle cancellation for call #{id}", ToString(), message.Id);
            if (EnsureUndisposed(throwException: false) && GetPendingCall(message.Id!.Value) is Call call)
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} cancelling call #{id}", ToString(), message.Id);
                    if (!call.IsDisposing)
                        call.CallCancellation.Cancel();
                }
                catch
                {
                }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Send an error response for a RPC request processing exception
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="exception">Exception</param>
        protected virtual async Task SendErrorResponseAsync(RequestMessage message, Exception exception)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} sending error response {type} for call #{id}", ToString(), exception.GetType(), message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't send error response for call #{id} when disposing", ToString(), message.Id);
                return;
            }
            try
            {
                await SendMessageAsync(new ErrorResponseMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = message.Id,
                    Error = exception
                }, RPC_PRIORTY).DynamicContext();
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} failed to send error response for call #{id}: {ex}", ToString(), message.Id, ex);
            }
        }

        /// <summary>
        /// Send a response for a RPC request
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="returnValue">Return value (should be <see langword="null"/>, if <see cref="RequestMessage.WantsReturnValue"/> is <see langword="false"/>)</param>
        protected virtual async Task SendResponseAsync(RequestMessage message, object? returnValue)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} sending response for call #{id}", ToString(), message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't send response for call #{id} when disposing", ToString(), message.Id);
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
                }, RPC_PRIORTY).DynamicContext();
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} failed to send response for call #{id}: {ex}", ToString(), message.Id, ex);
            }
        }

        /// <summary>
        /// RPC call
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        protected record class Call() : DisposableRecordBase()
        {
            /// <summary>
            /// RPC Processor
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
            /// Call cancellation
            /// </summary>
            public CancellationTokenSource CallCancellation { get; } = new();

            /// <summary>
            /// Completion
            /// </summary>
            public TaskCompletionSource<object?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Context
            /// </summary>
            public RpcContext? Context { get; set; }

            /// <summary>
            /// If the call was processed
            /// </summary>
            public bool Processed { get; set; }

            /// <summary>
            /// Incoming streams
            /// </summary>
            public HashSet<IncomingStream> Streams { get; } = [];

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (Done != DateTime.MinValue)
                    return;
                Done = DateTime.Now;
                Processor.Options.Logger?.Log(LogLevel.Debug, "{processor} RPC call #{id} processing done within {runtime}", Processor.ToString(), Message.Id, Done - Created);
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                CallCancellation.Cancel();
                CallCancellation.Dispose();
                Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (Streams.Count > 0)
                {
                    using (SemaphoreSyncContext ssc = Processor.IncomingStreamsSync)
                        foreach (IncomingStream stream in Streams)
                            Processor.RemoveIncomingStream(stream);
                    Streams.DisposeAll();
                }
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                CallCancellation.Cancel();
                CallCancellation.Dispose();
                Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (Streams.Count > 0)
                {
                    using (SemaphoreSyncContext ssc = await Processor.IncomingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (IncomingStream stream in Streams)
                            Processor.RemoveIncomingStream(stream);
                    await Streams.DisposeAllAsync().DynamicContext();
                }
            }
        }
    }
}
