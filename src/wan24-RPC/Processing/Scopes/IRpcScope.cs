using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;
using static wan24.RPC.Processing.RpcProcessor;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// Interface for a RPC scope (see <see cref="RpcProcessor.RpcScopeProcessorBase"/>)
    /// </summary>
    public interface IRpcScope : IWillDispose
    {
        /// <summary>
        /// RPC scope type ID
        /// </summary>
        int Type { get; }
        /// <summary>
        /// Name
        /// </summary>
        string? Name { get; set; }
        /// <summary>
        /// Scope ID
        /// </summary>
        long Id { get; }
        /// <summary>
        /// Scope key
        /// </summary>
        string? Key { get; }
        /// <summary>
        /// If the scope is stored
        /// </summary>
        bool IsStored { get; }
        /// <summary>
        /// If the scope was discarded from the peer
        /// </summary>
        bool IsDiscarded { get; }
        /// <summary>
        /// RPC processor
        /// </summary>
        RpcProcessor Processor { get; }
        /// <summary>
        /// Registered remote events
        /// </summary>
        IEnumerable<RpcScopeEvent> RemoteEvents { get; }
        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="arguments">Event arguments type (must be an <see cref="EventArgs"/>)</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        RpcScopeEvent RegisterEvent(in string name, in Type arguments, in RpcScopeEvent.EventHandler_Delegate handler);
        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <typeparam name="T">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        RpcScopeEvent RegisterEvent<T>(in string name, in RpcScopeEvent.EventHandler_Delegate handler) where T : EventArgs;
        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        RpcScopeEvent RegisterEvent(in string name, in RpcScopeEvent.EventHandler_Delegate handler);
        /// <summary>
        /// Raise an event at the peer
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="e">Event arguments</param>
        /// <param name="wait">Wait for remote event handlers to finish?</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default);
        /// <summary>
        /// Delegate for a remote event handler
        /// </summary>
        /// <param name="scope">RPC processor</param>
        /// <param name="message">Event message</param>
        public delegate void RemoteEventHandler_Delegate(RpcScopeProcessorBase scope, RemoteEventArgs message);
        /// <summary>
        /// Raised on remote event
        /// </summary>
        event RemoteEventHandler_Delegate? OnRemoteEvent;
        /// <summary>
        /// Remote event arguments
        /// </summary>
        /// <param name="e">Event handler</param>
        /// <param name="message">Event RPC message</param>
        public class RemoteEventArgs(in RpcScopeEvent? e, in IRpcScopeEventMessage message) : EventArgs()
        {
            /// <summary>
            /// Event handler
            /// </summary>
            public RpcScopeEvent? EventHandler { get; } = e;

            /// <summary>
            /// Event RPC message
            /// </summary>
            public IRpcScopeEventMessage Message { get; } = message;
        }
    }
}
