namespace wan24.RPC.Processing.Options
{
    /// <summary>
    /// RPC parallel queue options
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class ParallelQueueOptions()
    {
        /// <summary>
        /// Capacity
        /// </summary>
        public required int Capacity { get; init; }

        /// <summary>
        /// Number of threads that process queued items in parallel
        /// </summary>
        public required int Threads { get; init; }
    }
}
