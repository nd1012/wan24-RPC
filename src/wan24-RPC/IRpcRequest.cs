namespace wan24.RPC
{
    /// <summary>
    /// Interface for a RPC request
    /// </summary>
    public interface IRpcRequest
    {
        /// <summary>
        /// ID
        /// </summary>
        long Id { get; }
        /// <summary>
        /// API name
        /// </summary>
        string? Api { get; }
        /// <summary>
        /// API method name
        /// </summary>
        string Method { get; }
        /// <summary>
        /// API method parameters
        /// </summary>
        object?[]? Parameters { get; }
        /// <summary>
        /// If a response is being awaited (if <see langword="true"/>, a return value and/or an exception response will be awaited and processed)
        /// </summary>
        bool WantsResponse { get; }
    }
}
