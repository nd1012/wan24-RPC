namespace wan24.RPC
{
    /// <summary>
    /// RPC context
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RpcContext()
    {
        /// <summary>
        /// Cancellation
        /// </summary>
        public CancellationToken Cancellation { get; init; }
    }
}
