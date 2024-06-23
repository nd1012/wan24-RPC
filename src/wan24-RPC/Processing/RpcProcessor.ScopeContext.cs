using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Scope context
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
            /// Handle a RPC scope message (should throw on unknown message type)
            /// </summary>
            /// <param name="message">RPC scope message</param>
            /// <param name="cancellationToken">Cancellation token</param>
            public abstract Task HandleMessageAsync(IRpcScopeMessage message, CancellationToken cancellationToken);

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

            /// <inheritdoc/>
            protected override void Dispose(bool disposing) => Sync.Dispose();

            /// <inheritdoc/>
            protected override async Task DisposeCore() => await Sync.DisposeAsync().DynamicContext();
        }

        /// <summary>
        /// Base class for a RPC scope
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="id">ID</param>
        /// <param name="key">Key</param>
        public abstract record class RpcScopeBase(in RpcProcessor processor, in long id, in string? key = null) : RpcScopeProcessorBase(processor, key)
        {
            /// <summary>
            /// Dispose the <see cref="Value"/> when disposing?
            /// </summary>
            protected bool DisposeValue = true;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="processor">RPC processor</param>
            protected RpcScopeBase(in RpcProcessor processor) : this(processor, processor.CreateScopeId()) => processor.AddScope(this);

            /// <inheritdoc/>
            public sealed override long Id { get; } = id;

            /// <summary>
            /// Scope parameter
            /// </summary>
            public IRpcScopeParameter? ScopeParameter { get; init; }

            /// <summary>
            /// RPC method which returns the scope
            /// </summary>
            public RpcApiMethodInfo? Method { get; set; }

            /// <summary>
            /// Value
            /// </summary>
            public abstract object? Value { get; }

            /// <summary>
            /// If to dispose the <see cref="Value"/> on error
            /// </summary>
            public bool DisposeValueOnError { get; protected set; } = true;

            /// <summary>
            /// If the <see cref="Value"/> will be disposed when disposing
            /// </summary>
            public virtual bool WillDisposeValue => DisposeValue || (DisposeValueOnError && IsError);

            /// <summary>
            /// If to inform the scope consumer when disposing
            /// </summary>
            public bool InformConsumerWhenDisposing { get; set; } = true;

            /// <summary>
            /// If there was an error
            /// </summary>
            public bool IsError { get; protected set; }

            /// <summary>
            /// Last exception
            /// </summary>
            public Exception? LastException { get; protected set; }

            /// <summary>
            /// Set the value of <see cref="IsError"/> to <see langword="true"/> and perform required actions
            /// </summary>
            /// <param name="ex">Exception</param>
            [MemberNotNull(nameof(LastException))]
            public virtual async Task SetIsErrorAsync(Exception ex)
            {
                if (!EnsureUndisposed(allowDisposing: false, throwException: false))
                {
                    LastException ??= ex;
                    return;
                }
                Contract.Assume(LastException is not null);
                using (SemaphoreSyncContext ssc = await Sync.SyncContextAsync().DynamicContext())
                {
                    if (IsError)
                        return;
                    IsError = true;
                }
                LastException = ex;
                if (ScopeParameter is not null)
                    await ScopeParameter.SetIsErrorAsync().DynamicContext();
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                //TODO Signal the scope is not used anymore to the consumer
                if (IsStored)
                    Processor.RemoveScope(this);
                if (ScopeParameter is IDisposableObject disposable)
                {
                    disposable.Dispose();
                }
                else if (ScopeParameter?.ShouldDisposeScopeValue ?? false)
                {
                    ScopeParameter.DisposeScopeValueAsync().GetAwaiter().GetResult();
                }
                if (WillDisposeValue)
                    Value?.TryDispose();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                await base.DisposeCore().DynamicContext();
                //TODO Signal the scope is not used anymore to the consumer
                if (IsStored)
                    Processor.RemoveScope(this);
                if (ScopeParameter is IDisposableObject disposable)
                {
                    await disposable.DisposeAsync().DynamicContext();
                }
                else if (ScopeParameter?.ShouldDisposeScopeValue ?? false)
                {
                    await ScopeParameter.DisposeScopeValueAsync().DynamicContext();
                }
                if (WillDisposeValue && Value is object value)
                    await value.TryDisposeAsync().DynamicContext();
            }
        }

        /// <summary>
        /// Base class for a RPC remote scope
        /// </summary>
        public abstract record class RpcRemoteScopeBase : RpcScopeProcessorBase
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="processor">RPC processor</param>
            /// <param name="scope">Scope</param>
            /// <param name="add">Add the sope?</param>
            protected RpcRemoteScopeBase(in RpcProcessor processor, in RpcScopeValue scope, in bool add) : base(processor, scope.Key)
            {
                ScopeValue = scope;
                ReplaceExistingScope = scope.ReplaceExistingScope;
                IsStored = scope.IsStored;
                DisposeValue = scope.DisposeScopeValue;
                DisposeValueOnError = scope.DisposeScopeValueOnError;
                InformMasterWhenDisposing = scope.InformMasterWhenDisposing;
                if (add)
                    processor.AddRemoteScopeAsync(this).GetAwaiter().GetResult();
            }

            /// <summary>
            /// Received RPC scope value
            /// </summary>
            public RpcScopeValue ScopeValue { get; }

            /// <summary>
            /// RPC method parameter which got the scope
            /// </summary>
            public RpcApiMethodParameterInfo? Parameter { get; set; }

            /// <inheritdoc/>
            public sealed override long Id => ScopeValue.Id;

            /// <summary>
            /// If to replace the existing keyed remote scope
            /// </summary>
            public bool ReplaceExistingScope { get; }

            /// <summary>
            /// Value
            /// </summary>
            public virtual object? Value { get; }

            /// <summary>
            /// Dispose the <see cref="Value"/> when disposing?
            /// </summary>
            public bool DisposeValue { get; set; }

            /// <summary>
            /// If to dispose the <see cref="Value"/> on error
            /// </summary>
            public bool DisposeValueOnError { get; protected set; }

            /// <summary>
            /// If the <see cref="Value"/> will be disposed when disposing
            /// </summary>
            public virtual bool WillDisposeValue => DisposeValue || (DisposeValueOnError && IsError);

            /// <summary>
            /// If there was an error
            /// </summary>
            public bool IsError { get; protected set; }

            /// <summary>
            /// If to inform the scope master when disposing
            /// </summary>
            public bool InformMasterWhenDisposing { get; set; }

            /// <summary>
            /// Last exception
            /// </summary>
            public Exception? LastException { get; protected set; }

            /// <summary>
            /// Set the value of <see cref="IsError"/> to <see langword="true"/> and perform required actions
            /// </summary>
            /// <param name="ex">Exception</param>
            [MemberNotNull(nameof(LastException))]
            public virtual async Task SetIsErrorAsync(Exception ex)
            {
                if (!EnsureUndisposed(allowDisposing: false, throwException: false))
                {
                    LastException ??= ex;
                    return;
                }
                Contract.Assume(LastException is not null);
                using (SemaphoreSyncContext ssc = await Sync.SyncContextAsync().DynamicContext())
                {
                    if (IsError)
                        return;
                    IsError = true;
                }
                LastException = ex;
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                //TODO Signal the scope is not used anymore to the master
                if (IsStored)
                    Processor.RemoveRemoteScope(this);
                if (WillDisposeValue)
                    Value?.TryDispose();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                await base.DisposeCore().DynamicContext();
                //TODO Signal the scope is not used anymore to the master
                if (IsStored)
                    Processor.RemoveRemoteScope(this);
                if (WillDisposeValue && Value is object value)
                    await value.TryDisposeAsync().DynamicContext();
            }
        }
    }
}
