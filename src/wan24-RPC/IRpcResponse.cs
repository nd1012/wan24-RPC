namespace wan24.RPC
{
    /// <summary>
    /// Interface for a RPC response
    /// </summary>
    public interface IRpcResponse
    {
        /// <summary>
        /// ID
        /// </summary>
        long Id { get; }
        /// <summary>
        /// Return value
        /// </summary>
        object? ReturnValue { get; }
    }
}
