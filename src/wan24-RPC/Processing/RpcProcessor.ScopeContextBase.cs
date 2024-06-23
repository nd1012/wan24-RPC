using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Scopes;

namespace wan24.RPC.Processing
{
    // Scope context base
    public partial class RpcProcessor
    {
        /// <summary>
        /// Base class for a RPC scope processor
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="key">Key</param>
        public abstract record class RpcScopeProcessorBase(in RpcProcessor processor, in string? key = null) : DisposableRecordBase()
        {
            /// <summary>
            /// Thread synchronization
            /// </summary>
            protected readonly SemaphoreSync Sync = new();
            /// <summary>
            /// Remote events (key is the event name)
            /// </summary>
            protected readonly ConcurrentDictionary<string, RpcScopeEvent> _RemoteEvents = [];

            /// <summary>
            /// Name
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// Scope ID
            /// </summary>
            public abstract long Id { get; }

            /// <summary>
            /// Scope key
            /// </summary>
            public string? Key { get; } = key;

            /// <summary>
            /// If the scope is stored
            /// </summary>
            public bool IsStored { get; init; }

            /// <summary>
            /// If the scope was discarded from the peer
            /// </summary>
            public bool IsDiscarded { get; protected set; }

            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Logger
            /// </summary>
            public ILogger? Logger => Processor.Logger;

            /// <summary>
            /// RPC processor cancellation token
            /// </summary>
            public CancellationToken CancelToken => Processor.CancelToken;

            /// <summary>
            /// Register a remote event handler
            /// </summary>
            /// <param name="name">Event name</param>
            /// <param name="arguments">Event arguments type (must be an <see cref="EventArgs"/>)</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            public virtual RpcScopeEvent RegisterEvent(in string name, in Type arguments, in RpcScopeEvent.EventHandler_Delegate handler)
            {
                EnsureUndisposed();
                if (!typeof(EventArgs).IsAssignableFrom(arguments))
                    throw new ArgumentException("Invalid event arguments type", nameof(arguments));
                RpcScopeEvent e = CreateEvent(name, arguments, handler);
                if (!AddRemoteEvent(e))
                {
                    e.TryDispose();
                    throw new InvalidOperationException($"Event \"{name}\" handler registered already");
                }
                return e;
            }

            /// <summary>
            /// Register a remote event handler
            /// </summary>
            /// <typeparam name="T">Event arguments type</typeparam>
            /// <param name="name">Event name</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            public RpcScopeEvent RegisterEvent<T>(in string name, in RpcScopeEvent.EventHandler_Delegate handler) where T : EventArgs
                => RegisterEvent(name, typeof(T), handler);

            /// <summary>
            /// Register a remote event handler
            /// </summary>
            /// <param name="name">Event name</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            public virtual RpcScopeEvent RegisterEvent(in string name, in RpcScopeEvent.EventHandler_Delegate handler)
            {
                EnsureUndisposed();
                RpcScopeEvent e = CreateEvent(name, handler);
                if (!AddRemoteEvent(e))
                {
                    e.TryDispose();
                    throw new InvalidOperationException($"Event \"{name}\" handler registered already");
                }
                return e;
            }

            /// <summary>
            /// Raise an event at the peer
            /// </summary>
            /// <param name="name">Event name</param>
            /// <param name="e">Event arguments</param>
            /// <param name="wait">Wait for remote event handlers to finish?</param>
            /// <param name="cancellationToken">Cancellation token</param>
            public abstract Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default);

            /// <summary>
            /// Handle a RPC scope message (will throw on unknown message type to disconnect)
            /// </summary>
            /// <param name="message">RPC scope message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            public async Task HandleMessageAsync(IRpcScopeMessage message, CancellationToken cancellationToken)
            {
                await Task.Yield();
                Logger?.Log(LogLevel.Debug, "{this} handling message type {type}", ToString(), message.Type);
                try
                {
                    switch (message)
                    {
                        case IRpcScopeEventMessage remoteEvent:
                            await HandleEventAsync(remoteEvent, cancellationToken).DynamicContext();
                            return;
                        case IRpcScopeDiscardedMessage discardedMessage:
                            await HandleDiscardedAsync(discardedMessage, cancellationToken).DynamicContext();
                            return;
                    }
                    if(!await HandleMessageIntAsync(message, cancellationToken).DynamicContext())
                        throw new InvalidDataException($"Can't handle message type #{message.Id} ({message.GetType()})");
                }
                catch (ObjectDisposedException) when (IsDisposing || Processor.IsDisposing)
                {
                    Logger?.Log(LogLevel.Warning, "{this} handling message type {type} canceled due to disposing", ToString(), message.Type);
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    Logger?.Log(LogLevel.Warning, "{this} handling message type {type} canceled", ToString(), message.Type);
                }
            }

            /// <summary>
            /// Send a RPC message to the peer (using the outgoing message queue)
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="priority">Priority (higher value will be processed faster)</param>
            /// <param name="cancellationToken">Cancellation token</param>
            protected virtual Task SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken)
                => Processor.SendMessageAsync(message, priority, cancellationToken);

            /// <summary>
            /// Send a RPC message to the peer directly (won't use the outgoing message queue)
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            protected virtual Task SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken)
                => Processor.SendMessageAsync(message, cancellationToken);

            /// <summary>
            /// Add a remote event
            /// </summary>
            /// <param name="e">Event</param>
            protected virtual bool AddRemoteEvent(in RpcScopeEvent e)
                => _RemoteEvents.TryAdd(e.Name, e);

            /// <summary>
            /// Get a remote event
            /// </summary>
            /// <param name="name">Name</param>
            /// <returns>Event</returns>
            protected virtual RpcScopeEvent? GetRemoteEvent(in string name)
                => _RemoteEvents.TryGetValue(name, out RpcScopeEvent? res) ? res : null;

            /// <summary>
            /// Remove a remote event
            /// </summary>
            /// <param name="e">Event</param>
            /// <returns>If removed</returns>
            protected virtual bool RemoveRemoteEvent(in RpcScopeEvent e)
                => _RemoteEvents.TryRemove(e.Name, out _);

            /// <summary>
            /// Remove a remote event
            /// </summary>
            /// <param name="name">Name</param>
            /// <returns>Event</returns>
            protected virtual RpcScopeEvent? RemoveRemoteEvent(in string name)
                => _RemoteEvents.TryRemove(name, out RpcScopeEvent? res) ? res : null;

            /// <summary>
            /// Create an event
            /// </summary>
            /// <param name="name">Event name</param>
            /// <param name="arguments">Event arguments type</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            protected virtual RpcScopeEvent CreateEvent(in string name, in Type arguments, in RpcScopeEvent.EventHandler_Delegate handler)
                => new()
                {
                    Processor = Processor,
                    ScopeId = Id,
                    Name = name,
                    Arguments = arguments,
                    Handler = handler
                };

            /// <summary>
            /// Create an event
            /// </summary>
            /// <typeparam name="T">Event arguments type</typeparam>
            /// <param name="name">Event name</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            protected RpcScopeEvent CreateEvent<T>(in string name, in RpcScopeEvent.EventHandler_Delegate handler) where T : EventArgs
                => CreateEvent(name, typeof(T), handler);

            /// <summary>
            /// Create an event
            /// </summary>
            /// <param name="name">Event name</param>
            /// <param name="handler">Event handler</param>
            /// <returns>Event</returns>
            protected virtual RpcScopeEvent CreateEvent(in string name, in RpcScopeEvent.EventHandler_Delegate handler)
                => new()
                {
                    Processor = Processor,
                    ScopeId = Id,
                    Name = name,
                    Handler = handler
                };

            /// <summary>
            /// Handle a message (will disconnect when an exception is being thrown by this method)
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>If handled</returns>
            protected virtual Task<bool> HandleMessageIntAsync(IRpcScopeMessage message, CancellationToken cancellationToken) => Task.FromResult(false);

            /// <summary>
            /// Handle a RPC event (processing will be stopped on handler exception)
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            protected virtual async Task HandleEventAsync(IRpcScopeEventMessage message, CancellationToken cancellationToken)
            {
                Logger?.Log(LogLevel.Debug, "{this} handling event \"{name}\" with arguments type {type}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
                RpcScopeEvent? handler;
                try
                {
                    handler = GetRemoteEvent(message.Name);
                    if (handler is null)
                    {
                        Logger?.Log(LogLevel.Debug, "{this} no event \"{name}\" handler - ignoring", ToString(), message.Name);
                        return;
                    }
                    if (handler.Arguments is not null)
                        await message.DeserializeArgumentsAsync(handler.Arguments, cancellationToken).DynamicContext();
                    await handler.RaiseEventAsync(message, cancellationToken).DynamicContext();
                    Logger?.Log(LogLevel.Trace, "{this} handled event \"{name}\" with arguments type {type}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
                    if (message.Waiting)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" response", ToString(), message.Name);
                        await SendMessageAsync(new ResponseMessage()
                        {
                            PeerRpcVersion = Processor.Options.RpcVersion,
                            Id = message.Id
                        }, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
                    }
                }
                catch (ObjectDisposedException) when (IsDisposing || Processor.IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Logger?.Log(LogLevel.Warning, "{this} handling event \"{name}\" with arguments type {type} failed exceptional: {ex}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL", ex);
                    if (message.Waiting && !IsDisposing)
                        try
                        {
                            Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" error response", ToString(), message.Name);
                            await SendMessageAsync(new ErrorResponseMessage()
                            {
                                PeerRpcVersion = Processor.Options.RpcVersion,
                                Id = message.Id,
                                Error = ex
                            }, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
                        }
                        catch (Exception ex2)
                        {
                            Logger?.Log(LogLevel.Warning, "{this} sending event \"{name}\" error response failed: {ex2}", ToString(), message.Name, ex2);
                        }
                }
                finally
                {
                    await message.DisposeArgumentsAsync().DynamicContext();
                }
            }

            /// <summary>
            /// Handle discarded from the peer (should set <see cref="IsDiscarded"/> to <see langword="true"/> and dispose)
            /// </summary>
            /// <param name="message">Message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            protected virtual async Task HandleDiscardedAsync(IRpcScopeDiscardedMessage message, CancellationToken cancellationToken)
            {
                IsDiscarded = true;
                await DisposeAsync().DynamicContext();
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                // Remove events
                _RemoteEvents.Clear();
                // Others
                Sync.Dispose();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                // Remove events
                _RemoteEvents.Clear();
                // Others
                await Sync.DisposeAsync().DynamicContext();
            }
        }
    }
}
