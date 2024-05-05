using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// Remote RPC stream close message
    /// </summary>
    public class RemoteStreamCloseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 7;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <summary>
        /// Remote stream ID
        /// </summary>
        [Range(1, long.MaxValue)]
        public long Stream { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Stream, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Stream = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
