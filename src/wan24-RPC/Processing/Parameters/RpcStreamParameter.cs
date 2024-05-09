using wan24.Compression;
using wan24.Core;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// RPC stream parameter
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcStreamParameter() : DisposableRecordBase()
    {
        /// <summary>
        /// Source stream
        /// </summary>
        public required Stream Source { get; init; }

        /// <summary>
        /// If to dispose the <see cref="Source"/> stream after it was sent
        /// </summary>
        public bool DisposeSource { get; init; }

        /// <summary>
        /// Compression options
        /// </summary>
        public CompressionOptions? Compression { get; set; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (DisposeSource)
                Source.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (DisposeSource)
                await Source.DisposeAsync().DynamicContext();
        }
    }
}
