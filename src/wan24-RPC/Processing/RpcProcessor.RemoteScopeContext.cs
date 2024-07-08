using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

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
            public RpcScopeValue ScopeValue { get; }

            /// <inheritdoc/>
            public RpcApiMethodParameterInfo? Parameter { get; set; }

            /// <inheritdoc/>
            public sealed override long Id => ScopeValue.Id;

            /// <inheritdoc/>
            public bool ReplaceExistingScope { get; }

            /// <inheritdoc/>
            public virtual object? Value { get; }

            /// <inheritdoc/>
            public bool DisposeValue { get; set; }

            /// <inheritdoc/>
            public bool DisposeValueOnError { get; protected set; }

            /// <summary>
            /// If the <see cref="Value"/> will be disposed when disposing
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

            /// <inheritdoc/>
            public override string ToString() => $"{Processor}: Remote scope #{Id} ({GetType()})";

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
                        IsDiscarded = true;
                    }
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
                if (WillDisposeValue)
                    Value?.TryDispose();
                base.Dispose(disposing);
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                if (IsStored)
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
                if (WillDisposeValue && Value is object value)
                    await value.TryDisposeAsync().DynamicContext();
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
