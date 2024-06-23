namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Interface for a RPC scope event message
    /// </summary>
    public interface IRpcScopeEventMessage : IRpcScopeMessage, IRpcRequest
    {
        /// <summary>
        /// Event name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Event arguments (will be disposed at the receiver side)
        /// </summary>
        EventArgs? Arguments { get; }
        /// <summary>
        /// If the sender is waiting for the event handlers to finish
        /// </summary>
        bool Waiting { get; }
        /// <summary>
        /// Deserialize the arguments
        /// </summary>
        /// <param name="type">Arguments type (must be am <see cref="EventArgs"/>)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeserializeArgumentsAsync(Type type, CancellationToken cancellationToken = default);
        /// <summary>
        /// Dispose the arguments
        /// </summary>
        Task DisposeArgumentsAsync();
    }
}
