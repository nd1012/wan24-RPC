using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Api.Reflection.Extensions;

namespace wan24.RPC.Processing
{
    // Call queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC call queue
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        protected class CallQueue(in RpcProcessor processor)
            : ParallelItemQueueWorkerBase<Call>(processor.Options.CallQueueSize, processor.Options.CallThreads)
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <inheritdoc/>
            protected override async Task ProcessItem(Call item, CancellationToken cancellationToken)
            {
                if (item.Completion.Task.IsCompleted)
                    return;
                if (item.Message is not RequestMessage request)
                    throw new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)");
                using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.CallCancellation.Token);
                RpcApiMethodInfo method = request.Api is not null
                    ? Processor.Options.API.FindApi(request.Api)?.FindMethod(request.Method) ?? throw new InvalidDataException("API or method not found")
                    : Processor.Options.API.FindApiMethod(request.Method) ?? throw new InvalidDataException("API method not found");
                RpcContext context = GetContext(request, method, cancellation);
                await using (context.DynamicContext())
                {
                    item.Context = context;
                    context.Services.AddDiObject(context);
                    context.Services.AddDiObject(Processor);
                    _ = context.Services.AddDiObject(cancellation.Cancellation);
                    // Prepare parameters
                    // Call the method
                    // Handle the response
                    //TODO Set processed
                    //TODO 
                }
            }

            /// <summary>
            /// Get a context for processing a RPC call
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="method">API method</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>Context</returns>
            protected RpcContext GetContext(in RequestMessage message, in RpcApiMethodInfo method, in CancellationToken cancellationToken)
                => Processor.GetContext(message, method, cancellationToken);
        }
    }
}
