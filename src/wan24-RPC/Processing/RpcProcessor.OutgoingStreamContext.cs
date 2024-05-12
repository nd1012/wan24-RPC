using Microsoft.Extensions.Logging;
using wan24.Compression;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Outgoing stream context
    public partial class RpcProcessor
    {
        /// <summary>
        /// Outgoing stream (context)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        protected record class OutgoingStream() : DisposableRecordBase()
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public required RpcProcessor Processor { get; init; }

            /// <summary>
            /// Stream ID
            /// </summary>
            public long Id => Value.Stream ?? throw new InvalidProgramException("Missing outgoing stream ID");

            /// <summary>
            /// Parameter
            /// </summary>
            public required RpcOutgoingStreamParameter Parameter { get; init; }

            /// <summary>
            /// Value
            /// </summary>
            public required RpcStreamValue Value { get; init; }

            /// <summary>
            /// Pending request which sent this stream as parameter
            /// </summary>
            public Request? Request { get; set; }

            /// <summary>
            /// Pending call which returned this stream
            /// </summary>
            public Call? Call { get; set; }

            /// <summary>
            /// RPC API method which returned this stream
            /// </summary>
            public RpcApiMethodInfo? ApiMethod { get; set; }

            /// <summary>
            /// Cancellation
            /// </summary>
            public CancellationTokenSource Cancellation { get; } = new();

            /// <summary>
            /// If canceled from the remote peer
            /// </summary>
            public bool RemoteCanceled { get; set; }

            /// <summary>
            /// Source stream (which may be a buffer stream when using compression)
            /// </summary>
            public Stream? Source { get; protected set; }

            /// <summary>
            /// Started time
            /// </summary>
            public DateTime Started { get; protected set; } = DateTime.MinValue;

            /// <summary>
            /// If started
            /// </summary>
            public bool IsStarted => Started != DateTime.MinValue;

            /// <summary>
            /// Done time
            /// </summary>
            public DateTime Done { get; protected set; } = DateTime.MinValue;

            /// <summary>
            /// If done
            /// </summary>
            public bool IsDone => Done != DateTime.MinValue;

            /// <summary>
            /// Transfer completed?
            /// </summary>
            public bool Completed { get; protected set; }

            /// <summary>
            /// Last transfer exception
            /// </summary>
            public Exception? LastException { get; set; }

            /// <summary>
            /// Compression background task
            /// </summary>
            public Task? CompressionTask { get; protected set; }

            /// <summary>
            /// Start streaming
            /// </summary>
            /// <returns></returns>
            public virtual Task StartAsync()
            {
                EnsureUndisposed();
                if (Source is not null)
                    throw new InvalidOperationException("Streaming was started already");
                if (Parameter.Compression is null)
                {
                    Processor.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} without compression", Processor.ToString(), Id);
                    Source = Parameter.Source;
                }
                else
                {
                    Processor.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} with compression", Processor.ToString(), Id);
                    Source = new BlockingBufferStream(Processor.Options.CompressionBufferSize);
                    CompressionTask = CompressAsync();
                }
                return Task.CompletedTask;
            }

            /// <summary>
            /// Read the next chunk
            /// </summary>
            /// <param name="buffer">Buffer</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <returns>Number of bytes red</returns>
            public virtual async Task<int> ReadNextChunkAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                EnsureUndisposed();
                if (Source is null)
                    throw new InvalidProgramException("Streaming wasn't started");
                if (Completed)
                    throw new InvalidOperationException("Streaming has completed already");
                if (LastException is not null)
                    throw new AggregateException("Streaming has failed", LastException);
                try
                {
                    if (Equals(cancellationToken, default))
                        cancellationToken = Processor.CancelToken;
                    Processor.Logger?.Log(LogLevel.Trace, "{this} reading outgoing stream #{id} next chunk into {len} bytes buffer", Processor.ToString(), Id, buffer.Length);
                    using Cancellations cancellation = new(Processor.CancelToken, Cancellation.Token, cancellationToken);
                    int res = await Source.ReadAsync(buffer, cancellation).DynamicContext();
                    if (res != buffer.Length)
                    {
                        Processor.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} red last chunk with {len} bytes", Processor.ToString(), Id, res);
                        Completed = true;
                        SetDone();
                    }
                    else
                    {
                        Processor.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} red chunk with {len} bytes", Processor.ToString(), Id, res);
                    }
                    return res;
                }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
                {
                    Processor.Logger?.Log(LogLevel.Debug, "{this} reading outgoing stream #{id} next chunk canceled", Processor.ToString(), Id);
                    SetDone();
                    throw;
                }
                catch (Exception ex)
                {
                    Processor.Logger?.Log(LogLevel.Warning, "{this} reading outgoing stream #{id} next chunk failed: {ex}", Processor.ToString(), Id, ex);
                    LastException ??= ex;
                    SetDone();
                    if (!Processor.CancelToken.IsCancellationRequested)
                        try
                        {
                            await Processor.SendMessageAsync(new LocalStreamCloseMessage()
                            {
                                PeerRpcVersion = Processor.Options.RpcVersion,
                                Id = Id,
                                Error = ex
                            }, Processor.CancelToken).DynamicContext();
                        }
                        catch
                        {
                        }
                    throw;
                }
            }

            /// <summary>
            /// Set <see cref="Done"/>
            /// </summary>
            public virtual void SetDone()
            {
                if (Done == DateTime.MinValue)
                    Done = DateTime.Now;
            }

            /// <summary>
            /// Compression thread
            /// </summary>
            protected virtual async Task CompressAsync()
            {
                try
                {
                    await Task.Yield();
                    Processor.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} compression", Processor.ToString(), Id);
                    using Cancellations cancellation = new(Processor.CancelToken, Cancellation.Token);
                    BlockingBufferStream buffer = Source as BlockingBufferStream
                        ?? throw new InvalidProgramException("Outgoing source stream is not a BlockingBufferStream");
                    Stream compression = CompressionHelper.GetCompressionStream(buffer, Parameter.Compression);
                    await using (compression.DynamicContext())
                        await Parameter.Source.CopyToAsync(compression, Processor.Options.CompressionBufferSize, cancellation).DynamicContext();
                    await buffer.SetIsEndOfFileAsync(CancellationToken.None).DynamicContext();
                    Processor.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} compression done", Processor.ToString(), Id);
                }
                catch (ObjectDisposedException) when (IsDisposing || Processor.IsDisposing)
                {
                    Processor.Logger?.Log(LogLevel.Debug, "{this} outgoing stream #{id} compression canceled due to disposing state", Processor.ToString(), Id);
                }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
                {
                    Processor.Logger?.Log(LogLevel.Debug, "{this} outgoing stream #{id} compression canceled", Processor.ToString(), Id);
                }
                catch (Exception ex)
                {
                    Processor.Logger?.Log(LogLevel.Warning, "{this} outgoing stream #{id} compression failed: {ex}", Processor.ToString(), Id, ex);
                    LastException ??= ex;
                    SetDone();
                    if (!Processor.CancelToken.IsCancellationRequested)
                        try
                        {
                            await Processor.SendMessageAsync(new LocalStreamCloseMessage()
                            {
                                PeerRpcVersion = Processor.Options.RpcVersion,
                                Id = Id,
                                Error = ex
                            }, Processor.CancelToken).DynamicContext();
                        }
                        catch
                        {
                        }
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
                if (Source is not null && Source != Parameter.Source)
                    Source?.Dispose();
                SetDone();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
                if (Source is not null && Source != Parameter.Source)
                    await Source.DisposeAsync().DynamicContext();
                SetDone();
            }
        }
    }
}
