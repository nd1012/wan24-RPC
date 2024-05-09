using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// Interface for a RPC message
    /// </summary>
    public interface IRpcMessage : IStreamSerializer
    {
        /// <summary>
        /// Message Type ID
        /// </summary>
        int Type { get; }
        /// <summary>
        /// Message ID
        /// </summary>
        long? Id { get; }
        /// <summary>
        /// Created time
        /// </summary>
        DateTime Created { get; }
        /// <summary>
        /// Meta data (keys are limited to 255 characters; values are limited to 4KB string length)
        /// </summary>
        IReadOnlyDictionary<string, string> Meta { get; }
    }
}
