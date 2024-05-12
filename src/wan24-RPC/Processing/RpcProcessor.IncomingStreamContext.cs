using Microsoft.Extensions.Logging;
using wan24.Compression;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Incoming stream context
    public partial class RpcProcessor
    {
        /// <summary>
        /// Incoming stream (context)
        /// </summary>
        protected record class IncomingStream() : DisposableRecordBase(), IIncomingRpcStream
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public required RpcProcessor Processor { get; init; }

            /// <summary>
            /// Stream value
            /// </summary>
            public required RpcStreamValue Value { get; init; }

            /// <summary>
            /// Pending call which got this stream as parameter
            /// </summary>
            public Call? Call { get; set; }

            /// <summary>
            /// RPC API method parameter which got this stream
            /// </summary>
            public RpcApiMethodParameterInfo? ApiParameter { get; set; }

            /// <summary>
            /// Pending request which got this stream as return value
            /// </summary>
            public Request? Request { get; set; }

            /// <summary>
            /// RPC stream
            /// </summary>
            public required RpcStream Stream { get; init; }

            /// <summary>
            /// Chunk target stream
            /// </summary>
            public virtual Stream ChunkTarget => Decompression ?? Stream;

            /// <summary>
            /// Decompression stream
            /// </summary>
            public Stream? Decompression { get; protected set; }

            /// <summary>
            /// Last remote exception
            /// </summary>
            public Exception? LastRemoteException { get; protected set; }

            /// <summary>
            /// Streaming started time
            /// </summary>
            public DateTime Started { get; protected set; } = DateTime.MinValue;

            /// <inheritdoc/>
            public bool IsStarted => Started != DateTime.MinValue;

            /// <summary>
            /// Done time
            /// </summary>
            public DateTime Done { get; protected set; } = DateTime.MinValue;

            /// <inheritdoc/>
            public bool IsDone => Done != DateTime.MinValue;

            /// <summary>
            /// Runtime
            /// </summary>
            public TimeSpan Runtime => IsStarted
                ? IsDone
                    ? Done - Started
                    : DateTime.Now - Started
                : TimeSpan.Zero;

            /// <inheritdoc/>
            public bool IsCanceled { get; protected set; }

            /// <summary>
            /// If the next chunk was requested
            /// </summary>
            public bool IsChunkRequested { get; set; }

            /// <inheritdoc/>
            public virtual async Task StartAsync(CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                if (IsStarted)
                    throw new InvalidProgramException("Streaming started already");
                if (IsCanceled)
                    throw new InvalidOperationException("Streaming was canceled");
                Processor.Logger?.Log(LogLevel.Debug, "{this} starting incoming stream #{id}", Processor.ToString(), Value.Stream);
                IsChunkRequested = true;
                if (Value.Compression is CompressionOptions compression)
                {
                    Processor.Logger?.Log(LogLevel.Trace, "{this} starting incoming stream #{id} decompression", Processor.ToString(), Value.Stream);
                    compression.LeaveOpen = true;
                    //Decompression=CompressionHelper.GetDecompressionStream()
                    //TODO Decompression
                }
                Started = DateTime.Now;
                await Processor.SendMessageAsync(new StreamStartMessage()
                {
                    PeerRpcVersion = Processor.Options.RpcVersion,
                    Id = Value.Stream
                }, RPC_PRIORTY, cancellationToken).DynamicContext();
            }

            /// <summary>
            /// Complete streaming
            /// </summary>
            public virtual async Task CompleteAsync()
            {
                EnsureUndisposed();
                if (!IsStarted)
                    throw new InvalidProgramException("Streaming started already");
                if (IsCanceled)
                    throw new InvalidOperationException("Streaming was canceled");
                if (IsDone)
                    throw new InvalidOperationException("Streaming was done already");
                Processor.Logger?.Log(LogLevel.Debug, "{this} completing incoming stream #{id}", Processor.ToString(), Value.Stream);
                SetDone();
                if (Decompression is not null)
                    await Decompression.DisposeAsync().DynamicContext();
                if (!Stream.IsDisposing && !Stream.IsEndOfFile)
                    await Stream.SetIsEndOfFileAsync().DynamicContext();
            }

            /// <inheritdoc/>
            public virtual async Task CancelAsync()
            {
                if (!EnsureUndisposed(allowDisposing: true, throwException: false) || IsDone)
                    return;
                Processor.Logger?.Log(LogLevel.Debug, "{this} canceling incoming stream #{id}", Processor.ToString(), Value.Stream);
                IsCanceled = true;
                SetDone();
                if (!Processor.CancelToken.IsCancellationRequested)
                    try
                    {
                        await Processor.SendMessageAsync(new RemoteStreamCloseMessage()
                        {
                            PeerRpcVersion = Processor.Options.RpcVersion,
                            Id = Value.Stream
                        }, RPC_PRIORTY, Processor.CancelToken).DynamicContext();
                    }
                    catch
                    {
                    }
                if (!Stream.IsDisposing && !Stream.IsEndOfFile)
                    try
                    {
                        await Stream.SetIsEndOfFileAsync().DynamicContext();
                    }
                    catch
                    {
                    }
            }

            /// <summary>
            /// Set a remote exception
            /// </summary>
            /// <param name="message">Message</param>
            public virtual async Task SetRemoteExceptionAsync(LocalStreamCloseMessage message)
            {
                EnsureUndisposed();
                Processor.Logger?.Log(LogLevel.Debug, "{this} incoming stream #{id} had a remote exception: {ex}", Processor.ToString(), Value.Stream, message.Error);
                SetDone();
                LastRemoteException ??= message.Error;
                if (!Stream.IsDisposing && !Stream.IsEndOfFile)
                    try
                    {
                        await Stream.SetIsEndOfFileAsync(Processor.CancelToken).DynamicContext();
                    }
                    catch
                    {
                    }
            }

            /// <summary>
            /// Set <see cref="Done"/>
            /// </summary>
            public virtual void SetDone()
            {
                if (IsDone)
                    return;
                Done = DateTime.Now;
                Processor.Logger?.Log(LogLevel.Debug, "{this} incoming stream #{id} done within {runtime}", Processor.ToString(), Value.Stream, Runtime);
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (!IsDone)
                    CancelAsync().GetAwaiter().GetResult();
                Decompression?.Dispose();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                if (!IsDone)
                    await CancelAsync().DynamicContext();
                if (Decompression is not null)
                    await Decompression.DisposeAsync().DynamicContext();
            }
        }
    }
}
