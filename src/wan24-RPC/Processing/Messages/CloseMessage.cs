using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC close message (closing the connection)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class CloseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 16;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;

        /// <summary>
        /// Close reason code
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Close reason information
        /// </summary>
        [StringLength(byte.MaxValue)]
        public string? Info { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Code, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Info, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Code = await stream.ReadNumberAsync<int>(version, cancellationToken: cancellationToken).DynamicContext();
            Info = await stream.ReadStringNullableAsync(version, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
