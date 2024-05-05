using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// RPC stream chunk response message
    /// </summary>
    public class StreamChunkResponseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 6;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <summary>
        /// Chunk data
        /// </summary>
        public byte[] Data { get; set; } = null!;

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteBytesAsync(Data, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Data = (await stream.ReadBytesAsync(version, cancellationToken: cancellationToken).DynamicContext()).Value;
        }
    }
}
