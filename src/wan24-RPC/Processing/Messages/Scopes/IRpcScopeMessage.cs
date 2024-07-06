namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// RPC message for a scope
    /// </summary>
    public interface IRpcScopeMessage : IRpcMessage
    {
        /// <summary>
        /// If to fail the RPC processor if a message for an unknown scope was received
        /// </summary>
        bool FailOnScopeNotFound { get; }
        /// <summary>
        /// If to warn if a message for an unknown scope was received
        /// </summary>
        bool WarnOnScopeNotFound { get; }
        /// <summary>
        /// Scope ID
        /// </summary>
        long ScopeId { get; }
    }
}
