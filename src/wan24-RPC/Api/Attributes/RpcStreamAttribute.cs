using System.IO.Compression;
using wan24.Compression;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for a RPC API method stream return value transport configuration
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcStreamAttribute() : Attribute()
    {
        /// <summary>
        /// If to use compression
        /// </summary>
        public bool Compression { get; set; } = true;

        /// <summary>
        /// Used compression algorithm
        /// </summary>
        public string? CompressionAlgorithm { get; set; }

        /// <summary>
        /// Used compression flags
        /// </summary>
        public CompressionFlags? CompressionFlags { get; set; }

        /// <summary>
        /// Used comression level
        /// </summary>
        public CompressionLevel? CompressionLevel { get; set; }

        /// <summary>
        /// Apply the configuration to <see cref="CompressionOptions"/>
        /// </summary>
        /// <param name="options">Options</param>
        /// <returns>Options (may be a new instance or even <see langword="null"/>, if compression was disabled)</returns>
        public virtual CompressionOptions? ApplyTo(CompressionOptions? options)
        {
            if (!Compression)
                return null;
            options = CompressionHelper.GetDefaultOptions(options);
            if (CompressionAlgorithm is not null)
                options.WithAlgorithm(CompressionAlgorithm);
            if (CompressionFlags.HasValue)
                options.Flags = CompressionFlags.Value;
            if (CompressionLevel.HasValue)
                options.Level = CompressionLevel.Value;
            return options;
        }
    }
}
