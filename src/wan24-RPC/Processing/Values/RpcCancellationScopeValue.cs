using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Values
{
    /// <summary>
    /// RPC cancellation scope value
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcCancellationScopeValue() : RpcScopeValue()
    {
        /// <summary>
        /// Higher level object version
        /// </summary>
        public const int HL_VERSION = 1;

        /// <inheritdoc/>
        public override int HlObjectVersion => HL_VERSION;

        /// <summary>
        /// If the cancellation token was canceled
        /// </summary>
        public bool Canceled { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteAsync(Canceled, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Canceled = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
