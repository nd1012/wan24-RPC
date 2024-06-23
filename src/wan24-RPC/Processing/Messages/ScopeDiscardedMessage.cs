namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC scope was discarded (informational message to the scope consumer)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ScopeDiscardedMessage() : ScopeMessageBase(), IRpcRemoteScopeMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 7;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scope">Discarded RPC scope</param>
        public ScopeDiscardedMessage(in RpcProcessor.RpcScopeBase scope) : this() => ScopeId = scope.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;
    }
}
