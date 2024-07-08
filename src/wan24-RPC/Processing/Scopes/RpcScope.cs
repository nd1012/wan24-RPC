using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Parameters;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// Simple RPC scope (<see cref="RpcScopeTypes.Scope"/>)
    /// </summary>
    /// <param name="processor">RPC processor</param>
    /// <param name="key">Key</param>
    public class RpcScope(in RpcProcessor processor, in string? key = null) : RpcProcessor.RpcScopeInternalsBase(processor, key)
    {
        /// <summary>
        /// RPC scope type ID (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        public const int TYPE = (int)RpcScopeTypes.Scope;

        /// <summary>
        /// Value
        /// </summary>
        protected object? _Value = null;

        /// <summary>
        /// Maximum count of <see cref="Meta"/> entries (zero for no limit)
        /// </summary>
        public static int DefaultMaxMetaLength { get; set; } = byte.MaxValue;

        /// <inheritdoc/>
        public override int Type => (int)RpcScopeTypes.Scope;

        /// <inheritdoc/>
        public override object? Value => _Value;

        /// <summary>
        /// Maximum count of <see cref="Meta"/> entries (zero for no limit)
        /// </summary>
        public int MaxMetaLength { get; set; } = DefaultMaxMetaLength;

        /// <summary>
        /// Meta data (filled from trigger message meta data, if <see cref="UseTriggerMetaData"/> is <see langword="true"/>; disposable values will be disposed)
        /// </summary>
        public ConcurrentDictionary<string, object?> Meta { get; } = [];

        /// <summary>
        /// If to fill <see cref="Meta"/> from trigger message meta data
        /// </summary>
        public bool UseTriggerMetaData { get; set; } = true;

        /// <summary>
        /// Set the <see cref="Value"/>
        /// </summary>
        /// <param name="value">Value</param>
        public virtual void SetValue(in object? value)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            if (_Value is not null)
                throw new InvalidOperationException("A value has been set already");
            _Value = value;
        }

        /// <summary>
        /// Send a trigger to the peer
        /// </summary>
        /// <param name="trigger">Trigger</param>
        /// <param name="wait">If to wait for a response (will wait anyway, if <see cref="ScopeTriggerMessage.RequireId"/> is <see langword="true"/>, or <c>trigger</c> has 
        /// an ID)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task SendVoidTriggerAsync(ScopeTriggerMessage? trigger = null, bool wait = false, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (!trigger.Id.HasValue && (wait || trigger.RequireId))
                trigger.Id = CreateMessageId();
            if (trigger.Id.HasValue)
            {
                Logger?.Log(LogLevel.Debug, "{this} sending void trigger as #{id}", ToString(), trigger.Id);
                await SendVoidRequestAsync(trigger, useQueue: false, cancellationToken: cancellationToken).DynamicContext();
            }
            else
            {
                Logger?.Log(LogLevel.Debug, "{this} sending void trigger", ToString());
                await SendMessageAsync(trigger, Priorities.Event, cancellationToken: cancellationToken).DynamicContext();
            }
        }

        /// <summary>
        /// Send a trigger to the peer
        /// </summary>
        /// <param name="trigger">Trigger</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        [return: NotNull]
        public virtual async Task<T> SendTriggerAsync<T>(ScopeTriggerMessage? trigger = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (!trigger.Id.HasValue)
                trigger.Id = CreateMessageId();
            Logger?.Log(LogLevel.Debug, "{this} sending void trigger as #{id} (expect {type} return value)", ToString(), trigger.Id, typeof(T));
            return await SendRequestAsync<T>(trigger, useQueue: false, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Send a trigger to the peer
        /// </summary>
        /// <param name="trigger">Trigger</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        public virtual async Task<T?> SendTriggerNullableAsync<T>(ScopeTriggerMessage? trigger = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (!trigger.Id.HasValue)
                trigger.Id = CreateMessageId();
            Logger?.Log(LogLevel.Debug, "{this} sending void trigger as #{id} (expect nullable {type} return value)", ToString(), trigger.Id, typeof(T));
            return await SendRequestNullableAsync<T>(trigger, useQueue: false, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        public override void SetScopeParameter(in IRpcScopeParameter? parameter)
        {
            base.SetScopeParameter(parameter);
            if (_Value is null && parameter is RpcScopeParameter scopeParameter && scopeParameter.ScopeObject is not null)
                SetValue(scopeParameter.ScopeObject);
        }

        /// <inheritdoc/>
        protected override async Task<bool> HandleMessageIntAsync(IRpcScopeMessage message, CancellationToken cancellationToken)
        {
            Logger?.Log(LogLevel.Debug, "{this} try handling scope message #{id} ({type})", ToString(), message.Id, message.GetType());
            switch (message)
            {
                case RemoteScopeTriggerMessage trigger:
                    await HandleTriggerAsync(trigger, cancellationToken).DynamicContext();
                    break;
                default:
                    return false;
            }
            Logger?.Log(LogLevel.Trace, "{this} handled scope message #{id} ({type})", ToString(), message.Id, message.GetType());
            return true;
        }

        /// <summary>
        /// Handle a trigger message
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task HandleTriggerAsync(RemoteScopeTriggerMessage message, CancellationToken cancellationToken)
        {
            Logger?.Log(LogLevel.Debug, "{this} handling trigger message #{id}", ToString(), message.Id);
            if (UseTriggerMetaData)
            {
                if (MaxMetaLength > 0 && message.Meta.Count + Meta.Count > MaxMetaLength)
                    throw new OutOfMemoryException("Too much meta data");
                foreach (KeyValuePair<string, string> kvp in message.Meta)
                {
                    if (Meta.TryRemove(kvp.Key, out object? value))
                        await value.TryDisposeAsync().DynamicContext();
                    Meta.TryAdd(kvp.Key, kvp.Value);
                }
            }
            object? returnValue = RaiseOnTrigger(message);
            try
            {
                if (!message.Id.HasValue)
                    return;
                Logger?.Log(LogLevel.Trace, "{this} sending trigger message #{id} response {type}", ToString(), message.Id, returnValue?.GetType());
                await SendMessageAsync(new ResponseMessage()
                {
                    PeerRpcVersion = Processor.Options.RpcVersion,
                    Id = message.Id,
                    ReturnValue = returnValue
                }, Priorities.Rpc, cancellationToken).DynamicContext();
            }
            finally
            {
                if (returnValue is not null)
                    await returnValue.TryDisposeAsync().DynamicContext();
                Logger?.Log(LogLevel.Trace, "{this} handled trigger message #{id}", ToString(), message.Id);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Meta.Values.Where(v => v is not null)!.TryDisposeAll();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await base.DisposeCore().DynamicContext();
            await Meta.Values.Where(v => v is not null)!.TryDisposeAllAsync().DynamicContext();
        }

        /// <summary>
        /// Delegate for an <see cref="OnTrigger"/> event handler
        /// </summary>
        /// <param name="scope">Scope</param>
        /// <param name="e">Arguments</param>
        public delegate void TriggerHandler_Delegate(RpcScope scope, TriggerEventArgs e);
        /// <summary>
        /// Raised when a trigger message from the peer was received
        /// </summary>
        public event TriggerHandler_Delegate? OnTrigger;
        /// <summary>
        /// Raise the <see cref="OnTrigger"/> event
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>Return value</returns>
        protected virtual object? RaiseOnTrigger(RemoteScopeTriggerMessage message)
        {
            TriggerEventArgs e = new(message);
            OnTrigger?.Invoke(this, e);
            return e.ReturnValue;
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="parameter">RPC scope parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scope</returns>
        public static Task<RpcProcessor.RpcScopeBase> CreateAsync(RpcProcessor processor, IRpcScopeParameter parameter, CancellationToken cancellationToken = default)
            => Task.FromResult<RpcProcessor.RpcScopeBase>(new RpcScope(processor, parameter.Key)
            {
                ScopeParameter = parameter
            });

        /// <summary>
        /// <see cref="OnTrigger"/> event arguments
        /// </summary>
        /// <param name="message">Message</param>
        public class TriggerEventArgs(in RemoteScopeTriggerMessage message) : EventArgs()
        {
            /// <summary>
            /// Message
            /// </summary>
            public RemoteScopeTriggerMessage Message { get; } = message;

            /// <summary>
            /// Return value for the triggering peer (will be disposed)
            /// </summary>
            public object? ReturnValue { get; set; }
        }
    }
}
