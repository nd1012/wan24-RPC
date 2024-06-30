using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// Simple RPC remote scope (<see cref="RpcScopeTypes.Scope"/>)
    /// </summary>
    /// <param name="processor">RPC processor</param>
    /// <param name="value">RPC scope value</param>
    public class RpcRemoteScope(in RpcProcessor processor, in RpcScopeValue value) : RpcProcessor.RpcRemoteScopeInternalsBase(processor, value)
    {
        /// <inheritdoc/>
        public override int Type => (int)RpcScopeTypes.Scope;

        /// <summary>
        /// Send a trigger to the peer
        /// </summary>
        /// <param name="trigger">Trigger</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task SendTriggerAsync(RemoteScopeTriggerMessage? trigger = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            trigger ??= new()
            {
                ScopeId = Id,
                PeerRpcVersion = Processor.Options.RpcVersion
            };
            await SendMessageAsync(trigger, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task<bool> HandleMessageIntAsync(IRpcScopeMessage message, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case ScopeTriggerMessage trigger:
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
        protected virtual Task HandleTriggerAsync(ScopeTriggerMessage message, CancellationToken cancellationToken)
        {
            RaiseOnTrigger(message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delegate for an <see cref="OnTrigger"/> event handler
        /// </summary>
        /// <param name="scope">Scope</param>
        /// <param name="e">Arguments</param>
        public delegate void TriggerHandler_Delegate(RpcRemoteScope scope, TriggerEventArgs e);
        /// <summary>
        /// Raised when a trigger message from the peer was received
        /// </summary>
        public event TriggerHandler_Delegate? OnTrigger;
        /// <summary>
        /// Raise the <see cref="OnTrigger"/> event
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual void RaiseOnTrigger(ScopeTriggerMessage message) => OnTrigger?.Invoke(this, new(message));

        /// <summary>
        /// <see cref="OnTrigger"/> event arguments
        /// </summary>
        /// <param name="message">Message</param>
        public class TriggerEventArgs(in ScopeTriggerMessage message) : EventArgs()
        {
            /// <summary>
            /// Message
            /// </summary>
            public ScopeTriggerMessage Message { get; } = message;
        }
    }
}
