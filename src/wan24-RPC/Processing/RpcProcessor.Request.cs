using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Interfaces;

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
        /// Handle a RPC response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleResponseAsync(ResponseMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handling response for #{id}", this, message.Id);
            if (!EnsureUndisposed(throwException: false) || !PendingRequests.TryGetValue(message.Id!.Value, out Request? request))
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} can't handle response for #{id} (is disposing ({disposing}) or pending request not found)", this, message.Id, IsDisposing);
                await message.DisposeReturnValueAsync().DynamicContext();
                return;
            }
            if (request.Message is not RequestMessage requestMessage || requestMessage.WantsReturnValue)
            {
                request.ProcessorCompletion.TrySetResult(message.ReturnValue);
                return;
            }
            await message.DisposeReturnValueAsync().DynamicContext();
            if (message.ReturnValue is not null)
            {
                request.ProcessorCompletion.TrySetException(
                    new InvalidDataException($"Request #{request.Message.Id} didn't want a return value, but a return value was responded")
                    );
                return;
            }
            request.ProcessorCompletion.TrySetResult(result: null);
        }

        /// <summary>
        /// Handle a RPC error response (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual Task HandleErrorAsync(ErrorResponseMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handling error response for #{id}", this, message.Id);
            if (EnsureUndisposed(throwException: false) && PendingRequests.TryGetValue(message.Id!.Value, out Request? request))
                request.ProcessorCompletion.TrySetException(message.Error);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Cancel a RPC request
        /// </summary>
        /// <param name="request">RPC request message</param>
        protected virtual async Task CancelRequestAsync(RequestMessage request)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} canceling request #{id}", this, request.Id);
            if (!EnsureUndisposed(throwException: false))
                return;
            try
            {
                await SendMessageAsync(new CancellationMessage()
                {
                    Id = request.Id
                }).DynamicContext();
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} failed canceling request #{id}: {ex}", this, request.Id, ex);
            }

        }

        /// <summary>
        /// RPC request
        /// </summary>
        protected record class Request() : DisposableRecordBase(asyncDisposing: false)
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
            /// Processor completion
            /// </summary>
            public TaskCompletionSource<object?> ProcessorCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Request completion
            /// </summary>
            public TaskCompletionSource<object?> RequestCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// If the request was processed
            /// </summary>
            public bool Processed { get; set; }

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (Done == DateTime.MinValue)
                {
                    Done = DateTime.Now;
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{processor} RPC request #{id} processing done within {runtime}", Processor, Message.Id, Done - Created);
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                SetDone();
                if (RequestCompletion.Task.IsCompleted && ProcessorCompletion.Task.IsCompleted)
                    return;
                ObjectDisposedException exception = new(GetType().ToString());
                RequestCompletion.TrySetException(exception);
                ProcessorCompletion.TrySetException(exception);
            }
        }
    }
}
