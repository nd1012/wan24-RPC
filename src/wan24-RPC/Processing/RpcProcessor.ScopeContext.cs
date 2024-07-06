using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;

namespace wan24.RPC.Processing
{
    // Scope context
    public partial class RpcProcessor
    {
        /// <summary>
        /// Base class for a RPC scope
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="id">ID</param>
        /// <param name="key">Key</param>
        public abstract class RpcScopeBase(in RpcProcessor processor, in long id, in string? key = null) : RpcScopeProcessorBase(processor, key)
        {
            /// <summary>
            /// Dispose the <see cref="Value"/> when disposing?
            /// </summary>
            protected bool DisposeValue = true;
            /// <summary>
            /// Scope parameter
            /// </summary>
            protected IRpcScopeParameter? _ScopeParameter = null;
            /// <summary>
            /// If the scope is stored
            /// </summary>
            protected bool _IsStored = false;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="processor">RPC processor</param>
            /// <param name="key">Key</param>
            protected RpcScopeBase(in RpcProcessor processor, in string? key = null) : this(processor, processor.CreateScopeId(), key) { }

            /// <inheritdoc/>
            public sealed override long Id { get; } = id;

            /// <inheritdoc/>
            public override bool IsStored
            {
                get => _IsStored;
                set
                {
                    EnsureUndisposed();
                    EnsureNotDiscarded();
                    bool stored = Processor.GetScope(Id) is not null;
                    if (stored && !value)
                        throw new InvalidOperationException("Scope was stored, can't be unstored");
                    _IsStored = value;
                    if (value && !stored)
                        Processor.AddScope(this);
                }
            }

            /// <summary>
            /// Scope parameter
            /// </summary>
            public IRpcScopeParameter? ScopeParameter
            {
                get => _ScopeParameter;
                init => SetScopeParameter(value);
            }

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
            /// Create a scope parameter (should use <see cref="SetScopeParameter(in IRpcScopeParameter?)"/> to set the parameter to <see cref="ScopeParameter"/>)
            /// </summary>
            /// <param name="apiMethod">RPC API method (if <see langword="null"/>, the scope is being used as request parameter)</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <exception cref="InvalidOperationException">Not implemented</exception>
            [MemberNotNull(nameof(ScopeParameter))]
#pragma warning disable CS8774// Member must not be NULL
            public virtual async Task CreateScopeParameterAsync(RpcApiMethodInfo? apiMethod = null, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                EnsureNotDiscarded();
                if (RpcScopes.ScopeParameterFactories.TryGetValue(GetType(), out RpcScopes.ScopeParameterFactory_Delegate? parameterFactory))
                    SetScopeParameter(await parameterFactory.Invoke(Processor, this, apiMethod, cancellationToken).DynamicContext());
                if (_ScopeParameter is null)
                    throw new InvalidOperationException($"{GetType()} failed to create a scope parameter");
            }
#pragma warning restore CS8774// Member must not be NULL

            /// <summary>
            /// Set the <see cref="ScopeParameter"/>
            /// </summary>
            /// <param name="parameter">Scope parameter</param>
            /// <exception cref="InvalidOperationException"><see cref="ScopeParameter"/> was set already</exception>
            public virtual void SetScopeParameter(in IRpcScopeParameter? parameter)
            {
                EnsureUndisposed();
                EnsureNotDiscarded();
                if (_ScopeParameter is not null)
                    throw new InvalidOperationException("Scope parameter was set already");
                _ScopeParameter = parameter;
                if (parameter is null)
                    return;
                DisposeValueOnError = parameter.DisposeScopeValueOnError;
                IsStored = parameter.StoreScope;
            }

            /// <summary>
            /// Register this scope as a remote scope at the peer
            /// </summary>
            /// <param name="cancellationToken">Cancellation token</param>
            public virtual async Task RegisterRemoteAsync(CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                EnsureNotDiscarded();
                if (ScopeParameter is null)
                    await CreateScopeParameterAsync(cancellationToken: cancellationToken).DynamicContext();
                if (ScopeParameter.Value is null)
                    await ScopeParameter.CreateValueAsync(Processor, Id, cancellationToken).DynamicContext();
                ScopeParameter.Value.IsStored = true;
                ScopeParameter.Value.InformMasterWhenDisposing = true;
                if(!_IsStored)
                {
                    _IsStored = true;
                    Processor.AddScope(this);
                }
                await SendVoidRequestAsync(new ScopeRegistrationMessage()
                {
                    PeerRpcVersion = Processor.Options.RpcVersion,
                    Id = CreateMessageId(),
                    Value = ScopeParameter.Value
                }, cancellationToken).DynamicContext();
                InformConsumerWhenDisposing = true;
            }

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
            public override async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
                if (wait)
                {
                    Request request = new()
                    {
                        Processor = Processor,
                        Message = new ScopeEventMessage()
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
                    await SendMessageAsync(new ScopeEventMessage()
                    {
                        ScopeId = Id,
                        PeerRpcVersion = Processor.Options.RpcVersion,
                        Name = name,
                        Arguments = e
                    }, Priorities.Event, cancellationToken).DynamicContext();
                }
            }

            /// <inheritdoc/>
            public override string ToString() => $"{Processor}: Scope #{Id} ({GetType()})";

            /// <inheritdoc/>
            protected override async Task DiscardAsync(bool sync = true, CancellationToken cancellationToken = default)
            {
                using (SemaphoreSyncContext? ssc = sync ? await Sync.SyncContextAsync(cancellationToken).DynamicContext() : null)
                    try
                    {
                        if (IsDiscarded || ScopeParameter is null || Processor.IsDisposing || CancelToken.IsCancellationRequested)
                            return;
                    }
                    finally
                    {
                        IsDiscarded = true;
                    }
                await SendMessageAsync(new ScopeDiscardedMessage()
                {
                    ScopeId = Id,
                    PeerRpcVersion = Processor.Options.RpcVersion
                }, Priorities.Event, cancellationToken).DynamicContext();
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (IsStored)
                    Processor.RemoveScope(this);
                if (InformConsumerWhenDisposing)
                    try
                    {
                        DiscardAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log(LogLevel.Warning, "{this} failed to discard the scope at the peer: {ex}", ToString(), ex);
                    }
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
                base.Dispose(disposing);
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                if (IsStored)
                    Processor.RemoveScope(this);
                if (InformConsumerWhenDisposing)
                    try
                    {
                        await DiscardAsync().DynamicContext();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Log(LogLevel.Warning, "{this} failed to discard the scope at the peer: {ex}", ToString(), ex);
                    }
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
                await base.DisposeCore().DynamicContext();
            }
        }

        /// <summary>
        /// Base class for a RPC scope which exports its internals (explicit <see cref="IRpcProcessorInternals"/>)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        /// <param name="id">ID</param>
        /// <param name="key">Key</param>
        public abstract class RpcScopeInternalsBase(in RpcProcessor processor, in long id, in string? key = null)
            : RpcScopeBase(processor, id, key), IRpcScopeInternals
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="processor">RPC processor</param>
            /// <param name="key">Key</param>
            protected RpcScopeInternalsBase(in RpcProcessor processor, in string? key = null) : this(processor, processor.CreateScopeId(), key) => processor.AddScope(this);

            /// <inheritdoc/>
            long IRpcScopeInternals.CreateMessageId() => CreateMessageId();

            /// <inheritdoc/>
            Task<T> IRpcScopeInternals.SendRequestAsync<T>(IRpcRequest message, CancellationToken cancellationToken)
                => SendRequestAsync<T>(message, cancellationToken);

            /// <inheritdoc/>
            Task<T?> IRpcScopeInternals.SendRequestNullableAsync<T>(IRpcRequest message, CancellationToken cancellationToken) where T : default
                => SendRequestNullableAsync<T>(message, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendVoidRequestAsync(IRpcRequest message, CancellationToken cancellationToken)
                => SendVoidRequestAsync(message, cancellationToken);

            /// <inheritdoc/>
            Task<object?> IRpcScopeInternals.SendRequestNullableAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken)
                => SendRequestNullableAsync(message, returnType, cancellationToken);

            /// <inheritdoc/>
            Task<object> IRpcScopeInternals.SendRequestAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken)
                => SendRequestAsync(message, returnType, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken)
                => SendMessageAsync(message, priority, cancellationToken);

            /// <inheritdoc/>
            Task IRpcScopeInternals.SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken)
                => SendMessageAsync(message, cancellationToken);
        }
    }
}
