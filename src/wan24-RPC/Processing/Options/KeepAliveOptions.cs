namespace wan24.RPC.Processing.Options
{
    /// <summary>
    /// RPC keep alive options
    /// </summary>
    public record class KeepAliveOptions()
    {
        /// <summary>
        /// Heartbeat timeout
        /// </summary>
        public required TimeSpan Timeout { get; init; }

        /// <summary>
        /// Peer heartbeat timeout (only effective when <see cref="Timeout"/> was set to a value != <see cref="TimeSpan.Zero"/>)
        /// </summary>
        public TimeSpan PeerTimeout { get; init; } = TimeSpan.FromSeconds(1);
    }
}
