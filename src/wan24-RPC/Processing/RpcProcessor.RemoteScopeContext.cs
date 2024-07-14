using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;
using static wan24.Core.TranslationHelper;

namespace wan24.RPC.Processing
{
    // Remote scope context
    public partial class RpcProcessor
    {
        /// <summary>
        /// Base class for a RPC remote scope
        /// </summary>
        public abstract class RpcRemoteScopeBase : RpcScopeProcessorBase, IRpcRemoteScope
        {
            /// <summary>
            /// If <see cref="OnScopeCreated(CancellationToken)"/> should send a trigger message to the master scope after this scope was created
            /// </summary>
            protected bool HandleCreation = false;
            /// <summary>
            /// If <see cref="OnScopeCreated(CancellationToken)"/> has been called already
            /// </summary>
            protected bool CreationHandled = false;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="processor">RPC processor</param>
            /// <param name="scope">Scope</param>
            protected RpcRemoteScopeBase(in RpcProcessor processor, in RpcScopeValue scope) : base(processor, scope.Key)
            {
                ScopeValue = scope;
                ReplaceExistingScope = scope.ReplaceExistingScope;
                IsStored = scope.IsStored;
                DisposeValue = scope.DisposeScopeValue;
                DisposeValueOnError = scope.DisposeScopeValueOnError;
                InformMasterWhenDisposing = scope.InformMasterWhenDisposing;
            }

            /// <inheritdoc/>
            public override IEnumerable<Status> State
            {
                get
                {
                    foreach (Status status in base.State)
                        yield return status;
                    yield return new(__("Scope value"), ScopeValue.GetType(), __("The CLR type of the scope value from which this remote scope was created"));
                    yield return new(__("Parameter"), Parameter?.ToString(), __("The RPC API method parameter for which this remote scope was created"));
                    yield return new(__("Replace"), ReplaceExistingScope, __("If an existing named scope should be replaced by this scope"));
                    yield return new(__("Error"), IsError, __("If there was an error"));
                    yield return new(__("Exception"), LastException, __("The last exception"));
                    yield return new(__("Will dispose"), WillDisposeValue, __("If the hosted scope value will be disposed"));
                    yield return new(__("Inform"), InformMasterWhenDisposing, __("If the scope master should be informed when this scope is being discarded or disposed"));
                }
            }

            /// <inheritdoc/>
            public RpcScopeValue ScopeValue { get; }

            /// <inheritdoc/>
            public RpcApiMethodParameterInfo? Parameter { get; set; }

            /// <inheritdoc/>
            public sealed override long Id => ScopeValue.Id;

            /// <inheritdoc/>
            public bool ReplaceExistingScope { get; }

            /// <inheritdoc/>
            public override object? Value { get; }

            /// <inheritdoc/>
            public virtual bool DisposeValue
            {
                get => _DisposeValue;
                set => _DisposeValue = value;
            }

            /// <inheritdoc/>
            public bool DisposeValueOnError { get; protected set; }

            /// <summary>
            /// If the <see cref="RpcScopeProcessorBase.Value"/> will be disposed when disposing
            /// </summary>
            public virtual bool WillDisposeValue => DisposeValue || (DisposeValueOnError && IsError);

            /// <inheritdoc/>
            public bool IsError { get; protected set; }

            /// <inheritdoc/>
            public bool InformMasterWhenDisposing { get; set; }

            /// <inheritdoc/>
            public Exception? LastException { get; protected set; }

            /// <inheritdoc/>
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
            public override async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
                if (wait)
                {
                    Request request = new()
                    {
                        Processor = Processor,
                        Message = new RemoteScopeEventMessage()
                        {
                            ScopeId = Id,
                            PeerRpcVersion = Processor.Options.RpcVersion,
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
                        if (!Processor.AddPendingRequest(request))
                            throw new InvalidProgramException($"Failed to store event message #{request.Id} (double message ID)");
                        try
                        {
                            await SendMessageAsync(request.Message, Priorities.Event, cancellationToken).DynamicContext();
                            await request.ProcessorCompletion.Task.DynamicContext();
                        }
                        finally
                        {
                            Processor.RemovePendingRequest(request);
                        }
                    }
                }
                else
                {
                    await SendMessageAsync(new RemoteScopeEventMessage()
                    {
                        ScopeId = Id,
                        PeerRpcVersion = Processor.Options.RpcVersion,
                        Name = name,
                        Arguments = e
                    }, Priorities.Event, cancellationToken).DynamicContext();
                }
            }

            /// <summary>
            /// Handle scope created (and possibly stored)
            /// </summary>
            /// <param name="cancellationToken">Cancellation token</param>
            public virtual async Task OnScopeCreated(CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                EnsureNotDiscarded();
                if (CreationHandled)
                    throw new InvalidOperationException();
                CreationHandled = true;
                if (HandleCreation)
                    await SendMessageAsync(new RemoteScopeTriggerMessage()
                    {
                        PeerRpcVersion = Processor.Options.RpcVersion,
                        ScopeId = Id
                    }, Priorities.Event, cancellationToken).DynamicContext();
            }

            /// <inheritdoc/>
            public override string ToString() => $"{Processor}: Remote scope #{Id} ({GetType()})";

            /// <summary>
            /// Store the scope
            /// </summary>
            /// <returns>If stored</returns>
            protected virtual Task<bool> StoreScopeAsync() => Processor.AddRemoteScopeAsync(this);

            /// <inheritdoc/>
            protected override async Task DiscardAsync(bool sync = true, CancellationToken cancellationToken = default)
            {
                using (SemaphoreSyncContext? ssc = sync ? await Sync.SyncContextAsync(cancellationToken).DynamicContext() : null)
                    try
                    {
                        if (IsDiscarded || Processor.IsDisposing || CancelToken.IsCancellationRequested)
                            return;
                    }
                    finally
                    {
                        if (!IsDiscarded)
                            IsDiscarded = true;
                    }
                await DisposeValueAsync().DynamicContext();
                await SendMessageAsync(new RemoteScopeDiscardedMessage()
                {
                    ScopeId = Id,
                    PeerRpcVersion = Processor.Options.RpcVersion
                }, Priorities.Event, cancellationToken).DynamicContext();
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (IsStored)
                {
                    Processor.RemoveRemoteScope(this);
                    if (InformMasterWhenDisposing)
                        try
                        {
                            DiscardAsync().GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            Logger?.Log(LogLevel.Warning, "{this} failed to discard the scope at the peer: {ex}", ToString(), ex);
                        }
                }
                DisposeValueAsync().GetAwaiter().GetResult();
                base.Dispose(disposing);
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                if (IsStored)
                {
                    Processor.RemoveRemoteScope(this);
                    if (InformMasterWhenDisposing)
                        try
                        {
                            await DiscardAsync().DynamicContext();
                        }
                        catch (Exception ex)
                        {
                            Logger?.Log(LogLevel.Warning, "{this} failed to discard the scope at the peer: {ex}", ToString(), ex);
                        }
                }
                await DisposeValueAsync().DynamicContext();
                await base.DisposeCore().DynamicContext();
            }
        }

        /// <summary>
        /// Base class for a RPC remote scope wich exports its internals (explicit <see cref="IRpcProcessorInternals"/>)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="scope">Scope</param>
        public abstract class RpcRemoteScopeInternalsBase(in RpcProcessor processor, in RpcScopeValue scope)
            : RpcRemoteScopeBase(processor, scope), IRpcScopeInternals
        {

            /// <inheritdoc/>
            long IRpcScopeInternals.CreateMessageId() => CreateMessageId();

            /// <inheritdoc/>
            Task<T> IRpcScopeInternals.SendRequestAsync<T>(IRpcRequest message, bool useQueue, CancellationToken cancellationToken)
                => SendRequestAsync<T>(message, useQueue, cancellationToken);

            /// <inheritdoc/>
            Task<T?> IRpcScopeInternals.SendRequestNullableAsync<T>(IRpcRequest message, bool useQueue, CancellationToken cancellationToken) where T : default
                => SendRequestNullableAsync<T>(message, useQueue, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendVoidRequestAsync(IRpcRequest message, bool useQueue, CancellationToken cancellationToken)
                => SendVoidRequestAsync(message, useQueue, cancellationToken);

            /// <inheritdoc/>
            Task<object?> IRpcScopeInternals.SendRequestNullableAsync(IRpcRequest message, Type returnType, bool useQueue, CancellationToken cancellationToken)
                => SendRequestNullableAsync(message, returnType, useQueue, cancellationToken);

            /// <inheritdoc/>
            Task<object> IRpcScopeInternals.SendRequestAsync(IRpcRequest message, Type returnType, bool useQueue, CancellationToken cancellationToken)
                => SendRequestAsync(message, returnType, useQueue, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken)
                => SendMessageAsync(message, priority, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken)
                => SendMessageAsync(message, cancellationToken);
        }
    }
}
