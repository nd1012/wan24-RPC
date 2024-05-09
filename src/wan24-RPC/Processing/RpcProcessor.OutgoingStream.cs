using Microsoft.Extensions.Logging;
using wan24.Compression;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Outgoing stream
    public partial class RpcProcessor
    {
        /// <summary>
        /// Outgoing streams thread synchronization
        /// </summary>
        protected readonly SemaphoreSync OutgoingStreamsSync = new()
        {
            Name = "Outgoing RPC stream synchronization"
        };
        /// <summary>
        /// Outgoing streams (key is the stream ID)
        /// </summary>
        protected readonly Dictionary<long, OutgoingStream> OutgoingStreams = [];
        /// <summary>
        /// Outgoing stream ID
        /// </summary>
        protected long OutgoingStreamId = 0;

        /// <summary>
        /// Add an outgoing stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>If added</returns>
        protected virtual bool AddOutgoingStream(in OutgoingStream stream, in bool lockDict = false)
        {
            using SemaphoreSyncContext? ssc = lockDict ? OutgoingStreamsSync.SyncContext() : null;
            EnsureUndisposed();
            if (OutgoingStreams.Count > Options.MaxStreamCount)
                throw new TooManyRpcStreamsException("Maximum number of outgoing streams exceeded");
            return OutgoingStreams.TryAdd(stream.Id, stream);
        }

        /// <summary>
        /// Add an outgoing stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>If added</returns>
        protected virtual async Task<bool> AddOutgoingStreamAsync(OutgoingStream stream, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            using SemaphoreSyncContext? ssc = lockDict ? await OutgoingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed();
            if (OutgoingStreams.Count > Options.MaxStreamCount)
                throw new TooManyRpcStreamsException("Maximum number of outgoing streams exceeded");
            return OutgoingStreams.TryAdd(stream.Id, stream);
        }

        /// <summary>
        /// Get an outgoing stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>Stream</returns>
        protected virtual OutgoingStream? GetOutgoingStream(in long id, in bool lockDict = false)
        {
            using SemaphoreSyncContext? ssc = lockDict ? OutgoingStreamsSync.SyncContext() : null;
            EnsureUndisposed();
            return OutgoingStreams.TryGetValue(id, out OutgoingStream? res) ? res : null;
        }

        /// <summary>
        /// Get an outgoing stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream</returns>
        protected virtual async Task<OutgoingStream?> GetOutgoingStreamAsync(long id, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            using SemaphoreSyncContext? ssc = lockDict ? await OutgoingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed();
            return OutgoingStreams.TryGetValue(id, out OutgoingStream? res) ? res : null;
        }

        /// <summary>
        /// Remove an outgoing stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        protected virtual void RemoveOutgoingStream(in OutgoingStream stream, in bool lockDict = false)
        {
            using SemaphoreSyncContext? ssc = lockDict ? OutgoingStreamsSync.SyncContext() : null;
            EnsureUndisposed(allowDisposing: true);
            OutgoingStreams.Remove(stream.Id);
        }

        /// <summary>
        /// Remove an outgoing stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>Stream (don't forget to dispose!)</returns>
        protected virtual OutgoingStream? RemoveOutgoingStream(in long id, in bool lockDict = false)
        {
            using SemaphoreSyncContext? ssc = lockDict ? OutgoingStreamsSync.SyncContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!OutgoingStreams.TryGetValue(id, out OutgoingStream? res))
                return null;
            OutgoingStreams.Remove(id);
            return res;
        }

        /// <summary>
        /// Remove an outgoing stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task RemoveOutgoingStreamAsync(OutgoingStream stream, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            using SemaphoreSyncContext? ssc = lockDict ? await OutgoingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed(allowDisposing: true);
            OutgoingStreams.Remove(stream.Id);
        }

        /// <summary>
        /// Remove an outgoing stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream (don't forget to dispose!)</returns>
        protected virtual async Task<OutgoingStream?> RemoveOutgoingStreamAsync(long id, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            using SemaphoreSyncContext? ssc = lockDict ? await OutgoingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!OutgoingStreams.TryGetValue(id, out OutgoingStream? res))
                return null;
            OutgoingStreams.Remove(id);
            return res;
        }

        /// <summary>
        /// Create a RPC stream parameter
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="disposeStream">Dispose the stream after use?</param>
        /// <returns>Parameter</returns>
        protected virtual RpcStreamParameter CreateStreamParameter(in Stream stream, in bool disposeStream = false)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            CompressionOptions? compression = Options.DefaultCompression;
            if (compression is not null)
            {
                compression.LeaveOpen = true;
                if (stream.CanSeek)
                    compression.UncompressedDataLength = stream.Length - stream.Position;
            }
            return new()
            {
                Source = stream,
                DisposeSource = disposeStream,
                Compression = compression
            };
        }

        /// <summary>
        /// Create an outgoing stream
        /// </summary>
        /// <param name="streamParameter">Parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream value to send to the peer</returns>
        protected virtual async Task<RpcStreamValue> CreateOutgoingStreamAsync(RpcStreamParameter streamParameter, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            if (Equals(cancellationToken, default))
                cancellationToken = CancelToken;
            // If the stream content can fit into the stream value sent to the peer, send it directly
            long? len = streamParameter.Source.CanSeek
                ? streamParameter.Source.Length - streamParameter.Source.Position
                : null;
            if (len.HasValue && len.Value <= RpcStreamValue.MaxContentLength)
            {
                byte[] content = new byte[len.Value];
                await streamParameter.Source.ReadExactlyAsync(content, cancellationToken).DynamicContext();
                return new()
                {
                    Content = content
                };
            }
            // Prepare RPC streaming
            RpcStreamValue res = new()
            {
                Stream = Interlocked.Increment(ref OutgoingStreamId),
                Length = len,
                Compression = streamParameter.Compression
            };
            OutgoingStream stream = new()
            {
                Processor = this,
                Id = res.Stream.Value,
                Parameter = streamParameter,
                UncompressedSourceLength = streamParameter.Compression is null
                    ? len
                    : null
            };
            try
            {
                if (!await AddOutgoingStreamAsync(stream, cancellationToken: cancellationToken).DynamicContext())
                    throw new InvalidProgramException($"Can't store outgoing stream #{stream.Id} (double stream ID)");
                Options.Logger?.Log(LogLevel.Debug, "{this} created outgoing stream #{id}", ToString(), res.Stream);
                return res;
            }
            catch
            {
                await stream.DisposeAsync().DynamicContext();
                await res.DisposeAsync().DynamicContext();
                throw;
            }
        }

        /// <summary>
        /// Get the stream chunk length in bytes
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Chunk length in bytes</returns>
        protected virtual Task<int> GetStreamChunkLengthAsync(OutgoingStream stream, CancellationToken cancellationToken = default)
            => Task.FromResult(RpcStreamValue.MaxContentLength);

        /// <summary>
        /// Handle a stream start request (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleStreamStartAsync(StreamStartMessage message)
        {
            EnsureStreamsAreEnabled();
            if(await GetOutgoingStreamAsync(message.Id!.Value, cancellationToken: CancelToken).DynamicContext() is not OutgoingStream stream)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} got unknown outgoing stream #{id} start request", ToString(), message.Id);
                return;
            }
            Options.Logger?.Log(LogLevel.Debug, "{this} got outgoing stream #{id} start request", ToString(), message.Id);
            try
            {
                using Cancellations cancellations = new(CancelToken, stream.Cancellation.Token);
                await stream.StartAsync().DynamicContext();
                using RentedArrayStructSimple<byte> buffer = new(RpcStreamValue.MaxContentLength, clean: false);
                int red,// Number of chunk bytes red
                    len;// Number of chunk bytes expected
                long total = 0;// Total number of bytes red
                bool isLastChunk = false;// If this is the last chunk
                while (!isLastChunk && EnsureUndisposed())
                {
                    // Read the chunk
                    len = await GetStreamChunkLengthAsync(stream, cancellations).DynamicContext();
                    red = await stream.ReadNextChunkAsync(buffer[..len], cancellations).DynamicContext();
                    if (red > len)
                        throw new IOException($"Next outgoing stream chunk is larger than expected ({red}/{len} bytes)");
                    total += red;
                    if (stream.UncompressedSourceLength.HasValue && total > stream.UncompressedSourceLength.Value)
                        throw new IOException($"Total outgoing stream length is larger than expected ({total}/{stream.UncompressedSourceLength.Value} bytes)");
                    isLastChunk = red != len || (stream.UncompressedSourceLength.HasValue && total == stream.UncompressedSourceLength.Value);
                    // Send the chunk and wait for the peer to confirm
                    Options.Logger?.Log(LogLevel.Trace, "{this} sending outgoing stream #{id} chunk {len} bytes (last: {last})", ToString(), message.Id, red, isLastChunk);
                    Request request = new()
                    {
                        Processor = this,
                        Message = new StreamChunkMessage()
                        {
                            PeerRpcVersion = Options.RpcVersion,
                            Id = Interlocked.Increment(ref MessageId),
                            Stream = stream.Id,
                            Data = red == 0
                                ? null
                                : buffer[red..].ToArray(),
                            IsLastChunk = isLastChunk
                        },
                        ProcessorCancellation = cancellations
                    };
                    await using (request.DynamicContext())
                    {
                        if (!isLastChunk && !AddPendingRequest(request))
                            throw new InvalidProgramException($"Failed to store pending outgoing stream #{stream.Id} chunk request #{request.Message.Id} (double message ID)");
                        try
                        {
                            await SendMessageAsync(request.Message, CHUNK_PRIORTY, cancellations).DynamicContext();
                            if (!isLastChunk)
                            {
                                Options.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} waiting for chunk confirmation from the peer", ToString(), message.Id);
                                await request.ProcessorCompletion.Task.WaitAsync(cancellations).DynamicContext();
                            }
                        }
                        catch
                        {
                            RemovePendingRequest(request);
                            throw;
                        }
                        finally
                        {
                            if (!isLastChunk)
                                RemovePendingRequest(request);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stream.RemoteCanceled || CancelToken.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} sending outgoing stream #{id} chunks canceled", ToString(), message.Id);
            }
            catch (RpcRemoteException ex)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} sending outgoing stream #{id} chunks canceled due to a remote chunk handler error: {ex}", ToString(), message.Id, ex);
            }
            catch (Exception ex)
            {
                stream.LastException ??= ex;
                Options.Logger?.Log(LogLevel.Warning, "{this} sending outgoing stream #{id} chunks canceled due to a local error: {ex}", ToString(), message.Id, ex);
                if (!stream.IsDone)
                {
                    stream.SetDone();
                    try
                    {
                        await SendMessageAsync(new LocalStreamCloseMessage()
                        {
                            PeerRpcVersion = Options.RpcVersion,
                            Id = stream.Id,
                            Error = ex
                        }, CancelToken).DynamicContext();
                    }
                    catch//TODO Handle exception
                    {
                    }
                }
            }
            finally
            {
                stream.SetDone();
                Options.Logger?.Log(LogLevel.Debug, "{this} outgoing stream #{id} done within {runtime}", ToString(), message.Id, stream.Done - stream.Started);
                await (await RemoveOutgoingStreamAsync(stream.Id, cancellationToken: CancelToken).DynamicContext())!.DisposeAsync().DynamicContext();
            }
        }

        /// <summary>
        /// Handle a remote stream close request (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleRemoteStreamCloseAsync(RemoteStreamCloseMessage message)
        {
            EnsureStreamsAreEnabled();
            Options.Logger?.Log(LogLevel.Debug, "{this} got outgoing stream #{id} close request", ToString(), message.Id);
            if (await GetOutgoingStreamAsync(message.Id!.Value, cancellationToken: CancelToken).DynamicContext() is OutgoingStream stream)
                try
                {
                    if (!stream.IsDisposing && !stream.Cancellation.IsCancellationRequested)
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} set outgoing stream #{id} remote canceled", ToString(), message.Id);
                        stream.RemoteCanceled = true;
                        stream.Cancellation.Cancel();
                    }
                }
                catch
                {
                }
        }

        /// <summary>
        /// Outgoing stream
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
            public required long Id { get; init; }

            /// <summary>
            /// Parameter
            /// </summary>
            public required RpcStreamParameter Parameter { get; init; }

            /// <summary>
            /// Cancellation
            /// </summary>
            public CancellationTokenSource Cancellation { get; } = new();

            /// <summary>
            /// If canceled from the remote peer
            /// </summary>
            public bool RemoteCanceled { get; set; }

            /// <summary>
            /// Source stream
            /// </summary>
            public Stream? Source { get; protected set; }

            /// <summary>
            /// Uncompressed source stream length in bytes
            /// </summary>
            public long? UncompressedSourceLength { get; init; }

            /// <summary>
            /// Started time
            /// </summary>
            public DateTime Started { get; protected set; } = DateTime.MinValue;

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
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} without compression", Processor.ToString(), Id);
                    Source = Parameter.Source;
                }
                else
                {
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} with compression", Processor.ToString(), Id);
                    Source = new BlockingBufferStream(Processor.Options.CompressionBufferSize);
                    _ = CompressAsync();
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
                if (Source is null || Completed || LastException is not null)
                    throw new InvalidOperationException("Streaming wasn't started or is done already");
                try
                {
                    if (Equals(cancellationToken, default))
                        cancellationToken = Processor.CancelToken;
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} reading outgoing stream #{id} next chunk into {len} bytes buffer", Processor.ToString(), Id, buffer.Length);
                    using Cancellations cancellation = new(cancellationToken, Cancellation.Token);
                    int res = await Source.ReadAsync(buffer, cancellation).DynamicContext();
                    if (res != buffer.Length)
                    {
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} red last chunk with {len} bytes", Processor.ToString(), Id, res);
                        Completed = true;
                        SetDone();
                    }
                    else
                    {
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} red chunk with {len} bytes", Processor.ToString(), Id, res);
                    }
                    return res;
                }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
                {
                    Processor.Options.Logger?.Log(LogLevel.Debug, "{this} reading outgoing stream #{id} next chunk canceled", Processor.ToString(), Id);
                    SetDone();
                    throw;
                }
                catch (Exception ex)
                {
                    Processor.Options.Logger?.Log(LogLevel.Warning, "{this} reading outgoing stream #{id} next chunk failed: {ex}", Processor.ToString(), Id, ex);
                    LastException ??= ex;
                    SetDone();
                    try
                    {
                        await Processor.SendMessageAsync(new LocalStreamCloseMessage()
                        {
                            PeerRpcVersion = Processor.Options.RpcVersion,
                            Id = Id,
                            Error = ex
                        }, Processor.CancelToken).DynamicContext();
                    }
                    catch//TODO Handle exception
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
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} starting outgoing stream #{id} compression", Processor.ToString(), Id);
                    using Cancellations cancellation = new(Processor.CancelToken, Cancellation.Token);
                    BlockingBufferStream buffer = Source as BlockingBufferStream
                        ?? throw new InvalidProgramException("Outgoing source stream is not a blocking buffer stream");
                    Stream compression = CompressionHelper.GetCompressionStream(buffer, Parameter.Compression);
                    await using (compression.DynamicContext())
                        await Parameter.Source.CopyToAsync(compression, cancellation).DynamicContext();
                    await buffer.SetIsEndOfFileAsync(CancellationToken.None).DynamicContext();
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} compression done", Processor.ToString(), Id);
                }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
                {
                    Processor.Options.Logger?.Log(LogLevel.Debug, "{this} outgoing stream #{id} compression canceled", Processor.ToString(), Id);
                }
                catch (Exception ex)
                {
                    Processor.Options.Logger?.Log(LogLevel.Warning, "{this} outgoing stream #{id} compression failed: {ex}", Processor.ToString(), Id, ex);
                    LastException ??= ex;
                    SetDone();
                    try
                    {
                        await Processor.SendMessageAsync(new LocalStreamCloseMessage()
                        {
                            PeerRpcVersion = Processor.Options.RpcVersion,
                            Id = Id,
                            Error = ex
                        }, Processor.CancelToken).DynamicContext();
                    }
                    catch//TODO Handle exception
                    {
                    }
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                Cancellation.Cancel();
                Parameter.Dispose();
                Cancellation.Dispose();
                if (Source is not null && Source != Parameter.Source)
                    Source?.Dispose();
                SetDone();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                Cancellation.Cancel();
                await Parameter.DisposeAsync().DynamicContext();
                Cancellation.Dispose();
                if (Source is not null && Source != Parameter.Source)
                    await Source.DisposeAsync().DynamicContext();
                SetDone();
            }
        }
    }
}
