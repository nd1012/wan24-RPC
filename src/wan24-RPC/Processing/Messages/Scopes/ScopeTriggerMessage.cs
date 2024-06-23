namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Trigger a RPC scope at the consumer
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ScopeTriggerMessage() : ScopeMessageBase(), IRpcRemoteScopeMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 9;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scope">Triggered RPC scope</param>
        public ScopeTriggerMessage(in RpcProcessor.RpcScopeBase scope) : this() => ScopeId = scope.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;
    }
}
