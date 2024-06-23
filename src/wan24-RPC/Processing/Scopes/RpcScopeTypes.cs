namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope types enumeration (use <c>&gt;255</c> for your custom scope implementations)
    /// </summary>
    public enum RpcScopeTypes : int
    {
        /// <summary>
        /// Stream
        /// </summary>
        Stream = 0,
        /// <summary>
        /// Enumerable
        /// </summary>
        Enumerable = 1,
        /// <summary>
        /// Cancellation
        /// </summary>
        Cancellation = 2
    }
}
