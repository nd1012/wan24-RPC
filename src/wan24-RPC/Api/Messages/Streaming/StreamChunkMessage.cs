using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// RPC stream chunk message (if the response to this message is an error, the stream will be closed at the sender side)
    /// </summary>
    public class StreamChunkMessage() : RpcMessageBase(), IRpcRequest
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 6;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Stream ID
        /// </summary>
        [Range(1, long.MaxValue)]
        public long Stream { get; set; }

        /// <summary>
        /// Chunk data (if this is an empty array, the stream should be closed at the sender side)
        /// </summary>
        public byte[] Data { get; set; } = null!;

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Stream, cancellationToken).DynamicContext();
            await stream.WriteBytesAsync(Data, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Stream = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            Data = (await stream.ReadBytesAsync(version, cancellationToken: cancellationToken).DynamicContext()).Value;
        }
    }
}
