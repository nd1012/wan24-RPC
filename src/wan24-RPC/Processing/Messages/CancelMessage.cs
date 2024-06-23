namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC cancel message (cancels a RPC call)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class CancelMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 3;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request">RPC request to cancel</param>
        public CancelMessage(in RequestMessage request) : this() => Id = request.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;
    }
}
