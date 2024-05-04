using Microsoft.Extensions.Logging;
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
                await Task.Yield();
                Processor.Options.Logger?.Log(LogLevel.Debug, "{this} processing request #{id}", this, item.Message.Id);
                if (item.RequestCompletion.Task.IsCompleted)
                {
                    Processor.Options.Logger?.Log(LogLevel.Debug, "{this} request #{id} processed already", this, item.Message.Id);
                    return;
                }
                if (item.Message is not RequestMessage request)
                {
                    item.RequestCompletion.TrySetException(new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)"));
                    return;
                }
                using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.RequestCancellation);
                // Send the RPC request
                try
                {
                    await Processor.SendMessageAsync(request, cancellation).DynamicContext();
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
                    await Processor.CancelRequestAsync(request).DynamicContext();
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
                    //TODO Stream and enumeration
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
        }
    }
}
