using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Interfaces
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
    }
}
