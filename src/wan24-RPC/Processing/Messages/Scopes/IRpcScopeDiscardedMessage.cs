namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Interface for a RPC scope discarded message
    /// </summary>
    public interface IRpcScopeDiscardedMessage : IRpcScopeMessage
    {
        /// <summary>
        /// Discarded reason code
        /// </summary>
        int Code { get; }
        /// <summary>
        /// Discarded reason information
        /// </summary>
        string? Info { get; }
    }
}
