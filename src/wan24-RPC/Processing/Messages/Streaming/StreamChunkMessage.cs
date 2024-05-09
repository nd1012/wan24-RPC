using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Streaming
{
    /// <summary>
    /// RPC stream chunk message (if the response to this message is an error, the stream will be closed at the sender side; last chunk doesn't need a response)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
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
        [RequiredIf(nameof(IsLastChunk), false)]
        public byte[]? Data { get; set; }

        /// <summary>
        /// If this is the last chunk (stream is closed)
        /// </summary>
        public bool IsLastChunk { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Stream, cancellationToken).DynamicContext();
            await stream.WriteBytesNullableAsync(Data is null || Data.Length == 0 ? null : Data, cancellationToken).DynamicContext();
            if (Data is not null)
                await stream.WriteAsync(IsLastChunk, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Stream = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            Data = (await stream.ReadBytesNullableAsync(version, cancellationToken: cancellationToken).DynamicContext())?.Value;
            IsLastChunk = Data is null || Data.Length == 0 || await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
