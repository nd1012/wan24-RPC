using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC
{
    /// <summary>
    /// Base type for a RPC event message
    /// </summary>
    public abstract class RpcEventMessageBase() : RpcMessageBase()
    {
        /// <inheritdoc/>
        public sealed override bool RequireId => false;

        /// <summary>
        /// Event name
        /// </summary>
        [MinLength(1), MaxLength(byte.MaxValue)]
        public required string Name { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Name, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Name = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
