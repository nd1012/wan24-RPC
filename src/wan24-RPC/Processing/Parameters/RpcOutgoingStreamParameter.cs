using wan24.Compression;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// RPC stream parameter
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcOutgoingStreamParameter()
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

        /// <summary>
        /// If to dispose the RPC stream
        /// </summary>
        public bool DisposeRpcStream { get; init; }
    }
}
