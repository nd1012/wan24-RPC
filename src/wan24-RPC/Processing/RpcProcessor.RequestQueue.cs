using wan24.Core;
using wan24.RPC.Api.Messages;

namespace wan24.RPC.Processing
{
    // Request queue
    public partial class RpcProcessor
    {
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
                if (item.RequestCompletion.Task.IsCompleted)
                    return;
                if (item.Message is not RequestMessage request)
                    throw new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)");
                using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.RequestCancellation);
                // Send the RPC request
                try
                {
                    await SendMessageAsync(request, cancellation).DynamicContext();
                    if (!request.WantsResponse)
                    {
                        item.Processed = true;
                        item.SetDone();
                        item.RequestCompletion.TrySetResult(result: null);
                        return;
                    }
                }
                catch(Exception ex)
                {
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                    return;
                }
                // Await the response
                object? returnValue;
                try
                {
                    item.Processed = true;
                    returnValue = await item.ProcessorCompletion.Task.DynamicContext();
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == item.RequestCancellation)
                {
                    //TODO Send cancellation
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                    return;
                }
                catch (Exception ex)
                {
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                    return;
                }
                // Handle the response
                try
                {
                    //TODO
                    item.SetDone();
                    item.RequestCompletion.TrySetResult(returnValue);
                }
                catch (Exception ex)
                {
                    if (returnValue is not null)
                        await returnValue.TryDisposeAsync().DynamicContext();
                    item.SetDone();
                    item.RequestCompletion.TrySetException(ex);
                }
            }

            /// <summary>
            /// Send a RPC message to the peer
            /// </summary>
            /// <param name="request">RPC request message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            protected Task SendMessageAsync(RequestMessage request, CancellationToken cancellationToken)
                => Processor.SendMessageAsync(request, cancellationToken);

            /// <summary>
            /// Cancel a RPC request
            /// </summary>
            /// <param name="request">RPC request message</param>
            protected Task CancelRequestAsync(RequestMessage request) => Processor.CancelRequestAsync(request);
        }
    }
}
