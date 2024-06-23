﻿using wan24.RPC.Processing.Messages.Scopes;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope event
    /// </summary>
    public record class RpcScopeEvent
    {
        /// <summary>
        /// An object for thread synchronization
        /// </summary>
        protected readonly object SyncObject = new();

        /// <summary>
        /// Processor
        /// </summary>
        public required RpcProcessor Processor { get; init; }

        /// <summary>
        /// Scope ID
        /// </summary>
        public required long ScopeId { get; init; }

        /// <summary>
        /// Event name
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Event arguments type (must be a <see cref="EventArgs"/>)
        /// </summary>
        public Type? Arguments { get; init; }

        /// <summary>
        /// Event handler
        /// </summary>
        public required EventHandler_Delegate Handler { get; init; }

        /// <summary>
        /// Number of times the event was raised
        /// </summary>
        public long RaiseCount { get; protected set; }

        /// <summary>
        /// First raised time
        /// </summary>
        public DateTime FirstRaised { get; protected set; } = DateTime.MinValue;

        /// <summary>
        /// Last raised time
        /// </summary>
        public DateTime LastRaised { get; protected set; } = DateTime.MinValue;

        /// <summary>
        /// Raise the event
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual Task RaiseEventAsync(IRpcScopeEventMessage message, CancellationToken cancellationToken)
        {
            lock (SyncObject)
            {
                if (FirstRaised == DateTime.MinValue)
                    FirstRaised = DateTime.Now;
                LastRaised = DateTime.Now;
                RaiseCount++;
            }
            return Handler(this, message, cancellationToken);
        }

        /// <summary>
        /// Delegate for an event handler
        /// </summary>
        /// <param name="eventInfo">Event informations</param>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public delegate Task EventHandler_Delegate(RpcScopeEvent eventInfo, IRpcScopeEventMessage message, CancellationToken cancellationToken);
    }
}
