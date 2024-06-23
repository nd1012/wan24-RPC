﻿using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Parameters;

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
            public override async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
                if (wait)
                {
                    Request request = new()
                    {
                        Processor = Processor,
                        Message = new ScopeEventMessage(this)
                        {
                            ScopeId = Id,
                            PeerRpcVersion = Processor.Options.RpcVersion,
                            Id = Interlocked.Increment(ref Processor.MessageId),
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
                            await SendMessageAsync(request.Message, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
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
                    await SendMessageAsync(new ScopeEventMessage(this)
                    {
                        ScopeId = Id,
                        PeerRpcVersion = Processor.Options.RpcVersion,
                        Name = name,
                        Arguments = e
                    }, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
                }
            }

            /// <inheritdoc/>
            public override string ToString() => $"{Processor}: Scope #{Id} ({GetType()})";

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
    }
}