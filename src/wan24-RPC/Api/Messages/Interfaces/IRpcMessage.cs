namespace wan24.RPC.Api.Messages.Interfaces
{
    /// <summary>
    /// Interface fora RPC message
    /// </summary>
    public interface IRpcMessage
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
