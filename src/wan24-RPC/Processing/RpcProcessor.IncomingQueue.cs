using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;

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
            : ParallelItemQueueWorkerBase<IRpcMessage>(processor.Options.IncomingMessageQueue.Capacity, processor.Options.IncomingMessageQueue.Threads)
        {
            /// <summary>
            /// Space event (raised when having space for another incoming message)
            /// </summary>
            protected readonly ResetEvent SpaceEvent = new(initialState: true);

            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Logger
            /// </summary>
            public ILogger? Logger => Processor.Logger;

            /// <summary>
            /// If the queue has space
            /// </summary>
            public bool HasSpace => SpaceEvent.IsSet;

            /// <summary>
            /// Wait for space
            /// </summary>
            public virtual async Task WaitSpaceAsync()
            {
                EnsureUndisposed();
                using Cancellations cancellation = new(Processor.CancelToken, CancelToken);
                while (Queued >= Processor.Options.IncomingMessageQueue.Capacity)
                    await SpaceEvent.WaitAsync(cancellation).DynamicContext();
            }

            /// <summary>
            /// Reset the space event
            /// </summary>
            /// <param name="cancellationToken">Cancellation token</param>
            public virtual Task ResetSpaceEventAsync(CancellationToken cancellationToken = default)
                => SpaceEvent.ResetAsync(cancellationToken);

            /// <inheritdoc/>
            protected override async Task ProcessItem(IRpcMessage item, CancellationToken cancellationToken)
            {
                if (!SpaceEvent.IsSet && Queued < Processor.Options.IncomingMessageQueue.Capacity)
                {
                    Logger?.Log(LogLevel.Debug, "{this} signal having space for incoming messages", ToString());
                    await SpaceEvent.SetAsync(cancellationToken).DynamicContext();
                }
                Logger?.Log(LogLevel.Debug, "{this} processing incoming message type #{type} with ID #{id} ({message})", ToString(), item.Type, item.Id, item.GetType());
                await Processor.HandleIncomingMessageAsync(item).DynamicContext();
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
