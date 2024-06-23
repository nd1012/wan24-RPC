using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Remote scope context
    public partial class RpcProcessor
    {
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
            public override async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
                if (wait)
                {
                    Request request = new()
                    {
                        Processor = Processor,
                        Message = new RemoteScopeEventMessage(this)
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
                    await SendMessageAsync(new RemoteScopeEventMessage(this)
                    {
                        ScopeId = Id,
                        PeerRpcVersion = Processor.Options.RpcVersion,
                        Name = name,
                        Arguments = e
                    }, Processor.Options.Priorities.Event, cancellationToken).DynamicContext();
                }
            }

            /// <inheritdoc/>
            public override string ToString() => $"{Processor}: Remote scope #{Id} ({GetType()})";

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
