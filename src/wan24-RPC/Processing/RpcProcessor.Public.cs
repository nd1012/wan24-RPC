using Microsoft.Extensions.Logging;
using System.ComponentModel;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using static wan24.Core.TranslationHelper;

namespace wan24.RPC.Processing
{
    // Public methods/properties
    public partial class RpcProcessor
    {
        /// <summary>
        /// Time when the last message was received
        /// </summary>
        public virtual DateTime LastMessageReceived
        {
            get => _LastMessageReceived;
            set
            {
                if (!IsDisposing)
                    PeerHeartBeat?.Reset();
                _LastMessageReceived = value;
            }
        }

        /// <summary>
        /// Time when the last message was sent
        /// </summary>
        public virtual DateTime LastMessageSent
        {
            get => _LastMessageSent;
            set
            {
                if (!IsDisposing)
                    HeartBeat?.Reset();
                _LastMessageSent = value;
            }
        }

        /// <summary>
        /// Message loop duration (only available if <see cref="RpcProcessorOptions.KeepAlive"/> is used or <see cref="PingAsync(TimeSpan, CancellationToken)"/> was called)
        /// </summary>
        public TimeSpan MessageLoopDuration { get; protected set; }

        /// <summary>
        /// GUID
        /// </summary>
        public string GUID { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Options (will be disposed)
        /// </summary>
        public RpcProcessorOptions Options { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public virtual ILogger? Logger => Options?.Logger;

        /// <summary>
        /// Registered remote events
        /// </summary>
        public IEnumerable<RpcEvent> RemoteEvents => _RemoteEvents.Values;

        /// <summary>
        /// Number of stored scopes
        /// </summary>
        public int StoredScopeCount => _StoredScopeCount;

        /// <summary>
        /// Number of stored remote scopes
        /// </summary>
        public int StoredRemoteScopeCount => _StoredRemoteScopeCount;

        /// <inheritdoc/>
        public virtual IEnumerable<Status> State
        {
            get
            {
                yield return new(__("Type"), GetType(), __("RPC processor CLR type"));
                yield return new(__("Name"), Name, __("RPC processor name"));
                yield return new(__("Running"), IsRunning, __("If the RPC processor is running at present"));
                yield return new(__("Started"), Started == DateTime.MinValue ? __("(never)") : Started.ToString(), __("Started time"));
                yield return new(__("Stopped"), Stopped == DateTime.MinValue ? __("(never)") : Stopped.ToString(), __("Stopped time"));
                yield return new(__("Exception"), LastException?.Message ?? __("(none)"), __("Last exception"));
                yield return new(__("Scopes"), _StoredScopeCount, __("Number of RPC scopes"));
                yield return new(__("Remote scopes"), _StoredRemoteScopeCount, __("Number of RPC remote scopes"));
                yield return new(__("Calls"), PendingCalls.Count, __("Number of pending RPC calls"));
                yield return new(__("Requests"), PendingRequests.Count, __("Number of pending RPC requests"));
                yield return new(__("Events"), _RemoteEvents.Count, __("Number of registered remote event handlers"));
                foreach (Status status in IncomingMessages.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming message queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in Calls.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming calls queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in OutgoingMessages.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Outgoing message queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in Requests.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming requests queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
            }
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="returnValueType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        public virtual async Task<object?> CallValueAsync(
            string? api,
            string method,
            Type returnValueType,
            CancellationToken cancellationToken = default,
            params object?[] parameters
            )
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" method \"{method}\" at the peer ({count} parameters)", ToString(), api, method, parameters.Length);
            return await SendRequestAsync(new RequestMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId(),
                Api = api,
                Method = method,
                Parameters = parameters.Length == 0
                    ? null
                    : parameters
            }, returnValueType, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        public virtual async Task<T?> CallValueAsync<T>(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
            => (T?)await CallValueAsync(api, method, typeof(T), cancellationToken, parameters).DynamicContext();

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        public virtual async Task CallVoidAsync(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" void method \"{method}\" at the peer ({count} parameters)", ToString(), api, method, parameters.Length);
            await SendVoidRequestAsync(new RequestMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId(),
                Api = api,
                Method = method,
                Parameters = parameters.Length == 0
                    ? null
                    : parameters,
                WantsReturnValue = false
            }, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Send a ping message and wait for the pong message
        /// </summary>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [UserAction(), DisplayText("Ping"), Description("Send a ping message and wait for the pong response message")]
        public virtual async Task PingAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} sending ping request", ToString());
            using CancellationTokenSource cts = new(timeout);
            List<CancellationToken> token = [cts.Token];
            if (!cancellationToken.IsEqualTo(default))
                token.Add(cancellationToken);
            using Cancellations cancellation = new([.. token]);
            DateTime now = DateTime.Now;
            await SendVoidRequestAsync(new PingMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId()
            }, useQueue: false, cancellation).DynamicContext();
            TimeSpan runtime = DateTime.Now - now;
            MessageLoopDuration = runtime;
            Logger?.Log(LogLevel.Debug, "{this} got pong response after {runtime}", ToString(), runtime);
        }

        /// <summary>
        /// Send a close message to the peer and dispose
        /// </summary>
        /// <param name="code">Close reason code</param>
        /// <param name="info">Close reason information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [UserAction(MultiAction = true), DisplayText("Close"), Description("Close the connection and dispose")]
        public virtual async Task CloseAsync(int code = 0, string? info = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} stopping background services", ToString());
            bool isRunning = OutgoingMessages.IsRunning,
                sent = false;
            try
            {
                if (isRunning)
                {
                    await OutgoingMessages.StopAsync(cancellationToken).DynamicContext();
                    await IncomingMessages.StopAsync(cancellationToken).DynamicContext();
                }
                Logger?.Log(LogLevel.Debug, "{this} sending close message", ToString());
                await SendMessageAsync(new CloseMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Code = code,
                    Info = info
                }, cancellationToken).DynamicContext();
                sent = true;
                Logger?.Log(LogLevel.Debug, "{this} disposing after close message has been sent", ToString());
                await DisposeAsync().DynamicContext();
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} closing failed exceptional: {ex}", ToString(), ex);
                if (!isRunning || sent || CancelToken.IsCancellationRequested)
                    throw;
                Logger?.Log(LogLevel.Warning, "{this} resuming after error during closing", ToString());
                if (!OutgoingMessages.IsRunning)
                    await OutgoingMessages.StartAsync(CancellationToken.None).DynamicContext();
                if (!IncomingMessages.IsRunning)
                    await IncomingMessages.StartAsync(CancellationToken.None).DynamicContext();
                throw;
            }
        }

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="arguments">Event arguments type (must be an <see cref="EventArgs"/>)</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public virtual RpcEvent RegisterEvent(in string name, in Type arguments, in RpcEvent.EventHandler_Delegate handler)
        {
            EnsureUndisposed();
            if (!typeof(EventArgs).IsAssignableFrom(arguments))
                throw new ArgumentException("Invalid event arguments type", nameof(arguments));
            RpcEvent e = CreateEvent(name, arguments, handler);
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
        public RpcEvent RegisterEvent<T>(in string name, in RpcEvent.EventHandler_Delegate handler) where T : EventArgs
            => RegisterEvent(name, typeof(T), handler);

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public virtual RpcEvent RegisterEvent(in string name, in RpcEvent.EventHandler_Delegate handler)
        {
            EnsureUndisposed();
            RpcEvent e = CreateEvent(name, handler);
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
        [UserAction(MultiAction = true), DisplayText("Raise event"), Description("Raise an event at the peer")]
        public virtual async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
            if (wait)
            {
                Request request = new()
                {
                    Processor = this,
                    Message = new EventMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = CreateMessageId(),
                        Name = name,
                        Arguments = e,
                        Waiting = true
                    },
                    Cancellation = cancellationToken
                };
                await using (request.DynamicContext())
                {
                    Logger?.Log(LogLevel.Trace, "{this} storing event \"{name}\" request as #{id}", ToString(), name, request.Id);
                    if (!AddPendingRequest(request))
                        throw new InvalidProgramException($"Failed to store event message #{request.Id} (double message ID)");
                    try
                    {
                        await SendMessageAsync(request.Message, Options.Priorities.Event, cancellationToken).DynamicContext();
                        await request.ProcessorCompletion.Task.DynamicContext();
                    }
                    finally
                    {
                        RemovePendingRequest(request);
                    }
                }
            }
            else
            {
                await SendMessageAsync(new EventMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Name = name,
                    Arguments = e
                }, Options.Priorities.Event, cancellationToken).DynamicContext();
            }
        }

        /// <summary>
        /// Get a scope
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Scope (will be disposed)</returns>
        public virtual RpcScopeBase? GetScope(in string key)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return KeyedScopes.TryGetValue(key, out RpcScopeBase? res) ? res : null;
        }

        /// <summary>
        /// Get a remote scope
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Remote scope (will be disposed)</returns>
        public virtual RpcRemoteScopeBase? GetRemoteScope(in string key)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return KeyedRemoteScopes.TryGetValue(key, out RpcRemoteScopeBase? res) ? res : null;
        }

        /// <summary>
        /// Get the stored scope of a value
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Scope (will be disposed)</returns>
        public virtual RpcScopeBase? GetScopeOf(object value)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return Scopes.Values.FirstOrDefault(s => s.Value?.Equals(value) ?? false);
        }

        /// <summary>
        /// Get the stored remote scope of a value
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Remote scope (will be disposed)</returns>
        public virtual RpcRemoteScopeBase? GetRemoteScopeOf(object value)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return RemoteScopes.Values.FirstOrDefault(s => s.Value?.Equals(value) ?? false);
        }
    }
}
