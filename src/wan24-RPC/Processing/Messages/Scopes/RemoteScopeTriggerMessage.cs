namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Trigger a RPC scope at the master
    /// </summary>
    public class RemoteScopeTriggerMessage() : ScopeMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 10;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scope">Triggered remote RPC scope</param>
        public RemoteScopeTriggerMessage(in RpcProcessor.RpcRemoteScopeBase scope) : this() => ScopeId = scope.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;
    }
}
