namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC remote scope was discarded (informational message to the scope master)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RemoteScopeDiscardedMessage() : ScopeMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 8;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scope">Discarded remote RPC scope</param>
        public RemoteScopeDiscardedMessage(in RpcProcessor.RpcRemoteScopeBase scope) : this() => ScopeId = scope.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;
    }
}
