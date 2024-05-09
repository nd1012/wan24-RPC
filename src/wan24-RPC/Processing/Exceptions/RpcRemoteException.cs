namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// RPC remote exception (<see cref="Exception.InnerException"/> should serve the remote exception type)
    /// </summary>
    [Serializable]
    public class RpcRemoteException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RpcRemoteException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public RpcRemoteException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public RpcRemoteException(string? message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Remote exception type name
        /// </summary>
        public required string RemoteExceptionType { get; init; }
    }
}
