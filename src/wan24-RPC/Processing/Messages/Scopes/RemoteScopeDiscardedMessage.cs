namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// RPC remote scope was discarded (informational message to the scope master)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RemoteScopeDiscardedMessage() : ScopeMessageBase(), IRpcScopeDiscardedMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 8;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;

        /// <inheritdoc/>
        public sealed override bool FailOnScopeNotFound => false;
    }
}
