namespace wan24.RPC.Processing.Messages.Streaming
{
    /// <summary>
    /// Interface for a RPC stream message
    /// </summary>
    public interface IRpcStreamMessage : IRpcMessage
    {
        /// <summary>
        /// Stream ID
        /// </summary>
        long? Stream { get; }
    }
}
