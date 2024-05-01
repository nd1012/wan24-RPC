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
            if (!EnsureUndisposed(throwException: false) || !PendingRequests.TryGetValue(message.Id!.Value, out Request? request))
            {
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
            if (!EnsureUndisposed(throwException: false) || !request.Id.HasValue)
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
                //TODO Handle error
            }

        }

        /// <summary>
        /// RPC request
        /// </summary>
        protected record class Request() : DisposableRecordBase(asyncDisposing: false)
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
            /// Request cancellation token
            /// </summary>
            public CancellationToken RequestCancellation { get; init; }

            /// <summary>
            /// Processor completion
            /// </summary>
            public TaskCompletionSource<object?> ProcessorCompletion { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.LongRunning);

            /// <summary>
            /// Request completion
            /// </summary>
            public TaskCompletionSource<object?> RequestCompletion { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.LongRunning);

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
                    Done = DateTime.Now;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                ObjectDisposedException exception = new(GetType().ToString());
                RequestCompletion.TrySetException(exception);
                ProcessorCompletion.TrySetException(exception);
                SetDone();
            }
        }
    }
}
