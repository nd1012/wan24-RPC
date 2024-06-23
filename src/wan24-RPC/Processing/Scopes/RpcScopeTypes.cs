using wan24.Core;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope types enumeration (use <c>&gt;255</c> for your custom scope implementations)
    /// </summary>
    public enum RpcScopeTypes : int
    {
        /// <summary>
        /// Simple sope without any specific functionality
        /// </summary>
        [DisplayText("Scope")]
        Scope = 0,
        /// <summary>
        /// Stream
        /// </summary>
        [DisplayText("Stream")]
        Stream = 1,
        /// <summary>
        /// Enumerable
        /// </summary>
        [DisplayText("Enumerable")]
        Enumerable = 2,
        /// <summary>
        /// Cancellation
        /// </summary>
        [DisplayText("Cancellation")]
        Cancellation = 3
    }
}
