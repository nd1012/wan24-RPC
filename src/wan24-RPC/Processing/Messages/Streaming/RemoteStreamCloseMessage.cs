namespace wan24.RPC.Processing.Messages.Streaming
{
    /// <summary>
    /// Closes a remote RPC stream
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RemoteStreamCloseMessage() : RpcMessageBase(), IRpcStreamMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 7;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <inheritdoc/>
        long? IRpcStreamMessage.Stream => Id;
    }
}
