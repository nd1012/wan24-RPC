using wan24.Core;
using wan24.RPC.Api.Messages.Streaming;

namespace wan24.RPC.Processing
{
    // Outgoing stream
    public partial class RpcProcessor
    {
        /// <summary>
        /// Outgoing streams thread synchronization
        /// </summary>
        protected readonly SemaphoreSync OutgoingStreamsSync = new();
        /// <summary>
        /// Outgoing streams (key is the stream ID)
        /// </summary>
        protected readonly Dictionary<long, OutgoingStream> OutgoingStreams = [];
        /// <summary>
        /// Outgoing stream ID
        /// </summary>
        protected long OutgoingStreamId = 0;

        /// <summary>
        /// Start an outgoing stream
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Outgoing stream</returns>
        protected virtual async Task<OutgoingStream> StartOutgoingStreamAsync(Stream source, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            if (Options.MaxStreamCount < 1)
                throw new InvalidOperationException("Streams are disabled");
            if (cancellationToken == default)
                cancellationToken = CancelToken;
            using SemaphoreSyncContext ssc = await OutgoingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext();
            EnsureUndisposed();
            if (OutgoingStreams.Count > Options.MaxStreamCount)
                throw new InternalBufferOverflowException("Maximum number of outgoing streams exceeded");
            OutgoingStream res = new()
            {
                Processor = this,
                Id = Interlocked.Increment(ref OutgoingStreamId),
                Source = source
            };
            OutgoingStreams[res.Id] = res;
            return res;
        }

        /// <summary>
        /// Handle a stream start request
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleStreamStartAsync(StreamStartMessage message)
        {

        }

        /// <summary>
        /// Handle an outgoing stream close request
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleOutgoingStreamCloseAsync(RemoteStreamCloseMessage message)
        {

        }

        /// <summary>
        /// Outgoing stream
        /// </summary>
        protected record class OutgoingStream()
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public required RpcProcessor Processor { get; init; }

            /// <summary>
            /// Stream ID
            /// </summary>
            public required long Id { get; init; }

            /// <summary>
            /// Source stream
            /// </summary>
            public required Stream Source { get; init; }
        }
    }
}
