using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;

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
        /// Value
        /// </summary>
        protected object? _Value = null;

        /// <inheritdoc/>
        public override int Type => (int)RpcScopeTypes.Scope;

        /// <inheritdoc/>
        public override object? Value => _Value;

        /// <summary>
        /// Send a trigger to the peer
        /// </summary>
        /// <param name="trigger">Trigger</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task SendVoidTriggerAsync(ScopeTriggerMessage? trigger = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (trigger.Id.HasValue)
            {
                await SendVoidRequestAsync(trigger, cancellationToken).DynamicContext();
            }
            else
            {
                await SendMessageAsync(trigger, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
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
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (!trigger.Id.HasValue)
                trigger.Id = CreateMessageId();
            return await SendRequestAsync<T>(trigger, cancellationToken).DynamicContext();
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
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            if (!trigger.Id.HasValue)
                trigger.Id = CreateMessageId();
            return await SendRequestNullableAsync<T>(trigger, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Set the <see cref="Value"/>
        /// </summary>
        /// <param name="value">Value</param>
        public virtual void SetValue(in object? value)
        {
            EnsureUndisposed();
            _Value = value;
        }

        /// <inheritdoc/>
        protected override async Task<bool> HandleMessageIntAsync(IRpcScopeMessage message, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case RemoteScopeTriggerMessage trigger:
                    await HandleTriggerAsync(trigger, cancellationToken).DynamicContext();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Handle a trigger message
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual Task HandleTriggerAsync(RemoteScopeTriggerMessage message, CancellationToken cancellationToken)
        {
            RaiseOnTrigger(message);
            return Task.CompletedTask;
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
        protected virtual void RaiseOnTrigger(RemoteScopeTriggerMessage message) => OnTrigger?.Invoke(this, new(message));

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
        }
    }
}
