using Microsoft.Extensions.Logging;
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
            Options.Logger?.Log(LogLevel.Debug, "{this} handle call #{id}", this, message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't handle message (disposing)", this);
                Options.Logger?.Log(LogLevel.Trace, "{this} disposing parameters of call #{id}", this, message.Id);
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
                    Options.Logger?.Log(LogLevel.Warning, "{this} failed to add pending call #{id} (double message ID)", this, message.Id);
                    await SendErrorResponseAsync(message, new InvalidOperationException("Double message ID")).DynamicContext();
                    return;
                }
                removePending = true;
                // Try to enqueue the call for processing
                if (!Calls.TryEnqueue(call))
                {
                    Options.Logger?.Log(LogLevel.Warning, "{this} failed to enqueue call #{id} (too many queued calls)", this, message.Id);
                    PendingCalls.TryRemove(message.Id!.Value, out _);
                    await SendErrorResponseAsync(message, new TooManyRpcRequestsException("RPC request limit exceeded")).DynamicContext();
                    return;
                }
                // Wait for processing
                object? returnValue;
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} wait for processing call #{id}", this, message.Id);
                    returnValue = await call.Completion.Task.DynamicContext();
                    PendingCalls.TryRemove(message.Id!.Value, out _);
                }
                catch
                {
                    Options.Logger?.Log(LogLevel.Warning, "{this} call #{id} processing error", this, message.Id);
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
                    Options.Logger?.Log(LogLevel.Debug, "{this} send response for call #{id}", this, message.Id);
                    if (message.WantsReturnValue)
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} send response type \"{returnValue}\" for call #{id}", this, returnValue?.GetType(), message.Id);
                    }
                    else
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} doesn't want thr return value", this, message.Id);
                    }
                    await SendResponseAsync(message, message.WantsReturnValue ? returnValue : null).DynamicContext();
                }
                finally
                {
                    if (returnValue is not null && (call.Context is null || call.Context.Method.DisposeReturnValue))
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} disposing return value for call #{id}", this, message.Id);
                        await returnValue.TryDisposeAsync().DynamicContext();
                    }
                }
            }
            catch (OperationCanceledException ex) when (call.CallCancellation.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} cancelled during call #{id} processing", this, message.Id);
                await SendErrorResponseAsync(message, ex).DynamicContext();
            }
            catch (Exception ex)
            {
                if (!processingError || Options.DisconnectOnApiError)
                {
                    Options.Logger?.Log(LogLevel.Error, "{this} processing call #{id} failed with an exception: {ex}", this, message.Id, ex);
                    throw;
                }
                Options.Logger?.Log(LogLevel.Warning, "{this} processing call #{id} failed with an exception: {ex}", this, message.Id, ex);
                await HandleCallProcessingErrorAsync(call, ex).DynamicContext();
            }
            finally
            {
                if (removePending)
                    PendingCalls.TryRemove(message.Id!.Value, out _);
                Options.Logger?.Log(LogLevel.Trace, "{this} disposing parameters of call #{id} (processed: {processed})", this, message.Id, call.Processed);
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
            Options.Logger?.Log(LogLevel.Debug, "{this} handle cancellation for call #{id}", this, message.Id);
            if (EnsureUndisposed(throwException: false) && PendingCalls.TryGetValue(message.Id!.Value, out Call? call))
                try
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} cancelling call #{id}", this, message.Id);
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
            Options.Logger?.Log(LogLevel.Debug, "{this} sending error response {type} for call #{id}", this, exception.GetType(), message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't send error response for call #{id} (disposing)", this, message.Id);
                return;
            }
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
                Options.Logger?.Log(LogLevel.Error, "{this} failed to send error response for call #{id}: {ex}", this, message.Id, ex);
            }
        }

        /// <summary>
        /// Send a response for a RPC request
        /// </summary>
        /// <param name="message">RPC request</param>
        /// <param name="returnValue">Return value (should be <see langword="null"/>, if <see cref="RequestMessage.WantsReturnValue"/> is <see langword="false"/>)</param>
        protected virtual async Task SendResponseAsync(RequestMessage message, object? returnValue)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} sending response for call #{id}", this, message.Id);
            if (!EnsureUndisposed(throwException: false))
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} can't send response for call #{id} (disposing)", this, message.Id);
                return;
            }
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
                Options.Logger?.Log(LogLevel.Debug, "{this} failed to send response for call #{id}: {ex}", this, message.Id, ex);
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
