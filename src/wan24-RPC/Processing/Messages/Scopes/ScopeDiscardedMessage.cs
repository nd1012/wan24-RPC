namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// RPC scope was discarded (informational message to the scope consumer)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ScopeDiscardedMessage() : ScopeMessageBase(), IRpcScopeDiscardedMessage, IRpcRemoteScopeMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 7;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;

        /// <inheritdoc/>
        public sealed override bool FailOnScopeNotFound => false;
    }
}
