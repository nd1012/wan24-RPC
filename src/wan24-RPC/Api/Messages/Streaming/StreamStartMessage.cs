namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// Outgoing stream start request message
    /// </summary>
    public class StreamStartMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 5;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;
    }
}
