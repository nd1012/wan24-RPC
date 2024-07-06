namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC close message (closing the connection)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class CloseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 16;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;
    }
}
