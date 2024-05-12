namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// Ping message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class PingMessage() : RpcMessageBase(), IRpcRequest
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 9;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public override bool RequireId => true;
    }
}
