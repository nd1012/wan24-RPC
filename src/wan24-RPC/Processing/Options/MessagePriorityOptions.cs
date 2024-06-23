namespace wan24.RPC.Processing.Options
{
    /// <summary>
    /// RPC message priority options
    /// </summary>
    public record class MessagePriorityOptions()
    {
        /// <summary>
        /// RPC message (call/request/response/error) priority
        /// </summary>
        public int Rpc { get; init; } = 3_000;

        /// <summary>
        /// Event message priority
        /// </summary>
        public int Event { get; init; } = 2_000;

        /// <summary>
        /// Stream chunk message priority
        /// </summary>
        public int Chunk { get; init; } = 1_000;
    }
}
