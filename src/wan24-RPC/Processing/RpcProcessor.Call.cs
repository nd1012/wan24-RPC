using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Api.Exceptions;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Interfaces;

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
        /// Handle a RPC request (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleRequestAsync(RequestMessage message)
        {
            if (!EnsureUndisposed(throwException: false))
            {
                await message.DisposeParametersAsync().DynamicContext();
                return;
            }
            bool removePending = false,
                processingError = false;
            using Call call = new()
            {
                ProcessorCancellation = CancelToken,
                Message = message
            };
            try
            {
                // Store the call as pending (for handling a remote cancellation)
                if (!PendingCalls.TryAdd(message.Id!.Value, call))
                {
                    await SendErrorResponseAsync(message, new InvalidOperationException("Double message ID")).DynamicContext();
                    return;
                }
                removePending = true;
                // Try to enqueue the call for processing
                if (!Calls.TryEnqueue(call))
                {
                    PendingCalls.TryRemove(message.Id!.Value, out _);
                    await SendErrorResponseAsync(message, new TooManyRpcRequestsException("RPC request limit exceeded")).DynamicContext();
                    return;
                }
                // Wait for processing
                object? returnValue;
                try
                {
                    returnValue = await call.Completion.Task.DynamicContext();
                    PendingCalls.TryRemove(message.Id!.Value, out _);
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
                    if (message.WantsResponse)
                        await SendResponseAsync(message, message.WantsReturnValue ? returnValue : null).DynamicContext();
                }
                finally
                {
                    if (returnValue is not null && (call.Context is null || call.Context.Method.DisposeReturnValue))
                        await returnValue.TryDisposeAsync().DynamicContext();
                }
            }
            catch (OperationCanceledException ex) when (call.CallCancellation.IsCancellationRequested)
            {
                await SendErrorResponseAsync(message, ex).DynamicContext();
            }
            catch (Exception ex)
            {
                if (!processingError || Options.DisconnectOnApiError)
                    throw;
                await HandleCallProcessingErrorAsync(call, ex).DynamicContext();
            }
            finally
            {
                if (removePending)
                    PendingCalls.TryRemove(message.Id!.Value, out _);
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
            if (EnsureUndisposed(throwException: false) && PendingCalls.TryGetValue(message.Id!.Value, out Call? call))
                try
                {
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
            if (!EnsureUndisposed(throwException: false) || !message.WantsResponse)
                return;
            try
            {
                await SendMessageAsync(new ErrorResponseMessage()
                {
                    Id = message.Id,
                    Error = exception
                }).DynamicContext();
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                //TODO Handle error
            }
        }

        /// <summary>
        /// Send a response for a RPC request
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="returnValue">Return value (should be <see langword="null"/>, if <see cref="RequestMessage.WantsReturnValue"/> is <see langword="false"/>)</param>
        protected virtual async Task SendResponseAsync(RequestMessage message, object? returnValue)
        {
            if (!EnsureUndisposed(throwException: false) || !message.WantsResponse)
                return;
            try
            {
                await SendMessageAsync(new ResponseMessage()
                {
                    Id = message.Id,
                    ReturnValue = message.WantsReturnValue
                    ? returnValue
                    : null
                }).DynamicContext();
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                //TODO Handle error
            }
        }

        /// <summary>
        /// RPC call
        /// </summary>
        protected record class Call() : DisposableRecordBase(asyncDisposing: false)
        {
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
            public TaskCompletionSource<object?> Completion { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.LongRunning);

            /// <summary>
            /// Context
            /// </summary>
            public RpcContext? Context { get; set; }

            /// <summary>
            /// If the call was processed
            /// </summary>
            public bool Processed { get; set; }

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (Done == DateTime.MinValue)
                    Done = DateTime.Now;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                CallCancellation.Cancel();
                CallCancellation.Dispose();
                Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
            }
        }
    }
}
