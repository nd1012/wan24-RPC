namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// Closes a remote RPC stream
    /// </summary>
    public class RemoteStreamCloseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 7;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;
    }
}
