namespace wan24.RPC
{
    /// <summary>
    /// RPC cancellation message
    /// </summary>
    public class RpcCancellationMessage() : RpcResponseBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 1;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;
    }
}
