using wan24.Core;
using wan24.RPC.Processing.Messages;

/*
 * The number of queued incoming messages protects memory and CPU ressources and may be adjusted for your needs. Any overflow will stop reading incoming messages until 
 * a queued message was dequeued.
 */

//TODO Use a priority queue here, too?

namespace wan24.RPC.Processing
{
    // Incoming queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// Create an incoming messages queue
        /// </summary>
        /// <returns>Incoming messages queue</returns>
        protected virtual IncomingQueue CreateIncomingMessageQueue() => new(this)
        {
            Name = "Incoming RPC message queue"
        };

        /// <summary>
        /// Incoming message queue
        /// </summary>
        /// <param name="processor">RPC processor</param>
        protected class IncomingQueue(in RpcProcessor processor)
            : ParallelItemQueueWorkerBase<IRpcMessage>(processor.Options.IncomingMessageQueueCapacity, processor.Options.IncomingMessageQueueThreads)
        {
            /// <summary>
            /// Space event (raised when having space for another incoming message)
            /// </summary>
            public readonly ResetEvent SpaceEvent = new(initialState: true);

            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Wait for space
            /// </summary>
            public virtual async Task WaitSpaceAsync()
            {
                EnsureUndisposed();
                using Cancellations cancellation = new(Processor.CancelToken, CancelToken);
                while (Queued >= Processor.Options.IncomingMessageQueueCapacity)
                    await SpaceEvent.WaitAsync(cancellation).DynamicContext();
            }

            /// <inheritdoc/>
            protected override async Task ProcessItem(IRpcMessage item, CancellationToken cancellationToken)
            {
                if (Queued < Processor.Options.IncomingMessageQueueCapacity)
                    await SpaceEvent.SetAsync(cancellationToken).DynamicContext();
                await Processor.HandleMessageAsync(item).DynamicContext();
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                SpaceEvent.Dispose();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                await base.DisposeCore().DynamicContext();
                await SpaceEvent.DisposeAsync().DynamicContext();
            }
        }
    }
}
