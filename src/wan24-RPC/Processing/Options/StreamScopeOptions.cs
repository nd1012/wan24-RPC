using wan24.Compression;

namespace wan24.RPC.Processing.Options
{
    /// <summary>
    /// RPC stream scope options
    /// </summary>
    public record class StreamScopeOptions()
    {
        /// <summary>
        /// Max. stream content length in bytes
        /// </summary>
        public static int MaxStreamContentLength { get; set; } = ushort.MaxValue;

        /// <summary>
        /// Default compression options for streams
        /// </summary>
        public CompressionOptions? DefaultCompression { get; init; }

        /// <summary>
        /// Compression buffer size in bytes
        /// </summary>
        public int CompressionBufferSize { get; init; } = MaxStreamContentLength;

        /// <summary>
        /// Decompression buffer size in bytes
        /// </summary>
        public int DecompressionBufferSize { get; init; } = MaxStreamContentLength;
    }
}
