namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// Pong message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class PongMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 10;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ping">Ping message</param>
        public PongMessage(in PingMessage ping) : this() => Id = ping.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public override bool RequireId => true;
    }
}
