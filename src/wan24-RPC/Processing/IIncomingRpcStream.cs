namespace wan24.RPC.Processing
{
    /// <summary>
    /// Interface for an incoming RPC stream context
    /// </summary>
    public interface IIncomingRpcStream
    {
        /// <summary>
        /// If disposed
        /// </summary>
        bool IsDisposed { get; }
        /// <summary>
        /// If started
        /// </summary>
        bool IsStarted { get; }
        /// <summary>
        /// If canceled
        /// </summary>
        bool IsCanceled { get; }
        /// <summary>
        /// If done
        /// </summary>
        bool IsDone { get; }
        /// <summary>
        /// The last remote exception
        /// </summary>
        Exception? LastRemoteException { get; }
        /// <summary>
        /// Start streaming
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StartAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Cancel streaming
        /// </summary>
        Task CancelAsync();
    }
}
