namespace wan24.RPC.Processing.Messages.Streaming
{
    /// <summary>
    /// Signal local RPC stream close to the peer due to an error
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class LocalStreamCloseMessage() : ErrorResponseMessage(), IRpcStreamMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int HL_TYPE_ID = 8;

        /// <inheritdoc/>
        public override int Type => HL_TYPE_ID;

        /// <inheritdoc/>
        long? IRpcStreamMessage.Stream => Id;
    }
}
