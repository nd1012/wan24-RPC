using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

/*
 * The usual asynchronous streaming process is:
 * 
 * 1. A stream parameter or return value must be sent to the peer
 * 2. An outgoing stream will be initialized and stored
 * 3. The peer requests start sending the stream
 * 4. The RPC processor sends all stream chunks
 * 5. The RPC processor removes the outgoing stream (and disposes the source, if required)
 * 
 * The peer may interrupt the stream by sending a remote close message, which will stop sending stream chunks.
 * 
 * The RPC processor will send a local close message to the peer on cancellation or error and remove the outgoing stream.
 * 
 * The number of outgoing streams is limited to protect memory ressources. It should be equal to the max. number of incoming streams at the peer.
 */

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
        /// <param name="disposeRpcStream">Dispose the RPC stream from the RPC processor?</param>
        /// <returns>Parameter</returns>
        protected virtual RpcOutgoingStreamParameter CreateOutgoingStreamParameter(in Stream stream, in bool disposeStream = false, in bool disposeRpcStream = false)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            return new()
            {
                Source = stream,
                DisposeSource = disposeStream,
                DisposeRpcStream = disposeRpcStream,
                Compression = Options.DefaultCompression is null
                    ? null
                    : Options.DefaultCompression with
                    {
                        LeaveOpen = true,
                        UncompressedDataLength = stream.CanSeek
                            ? stream.Length - stream.Position
                            : -1
                    }
            };
        }

        /// <summary>
        /// Create an outgoing stream
        /// </summary>
        /// <param name="streamParameter">Parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream value to send to the peer</returns>
        protected virtual async Task<RpcStreamValue> CreateOutgoingStreamAsync(RpcOutgoingStreamParameter streamParameter, CancellationToken cancellationToken = default)
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
            // Create an outgoing RPC stream
            RpcStreamValue res = new()
            {
                Stream = Interlocked.Increment(ref OutgoingStreamId),
                Length = len,
                Compression = streamParameter.Compression
            };
            OutgoingStream stream = new()
            {
                Processor = this,
                Parameter = streamParameter,
                Value = res
            };
            try
            {
                if (!await AddOutgoingStreamAsync(stream, cancellationToken: cancellationToken).DynamicContext())
                    throw new InvalidProgramException($"Can't store outgoing stream #{stream.Id} (double stream ID)");
                Logger?.Log(LogLevel.Debug, "{this} created outgoing stream #{id}", ToString(), res.Stream);
                return res;
            }
            catch
            {
                await stream.DisposeAsync().DynamicContext();
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
                Logger?.Log(LogLevel.Warning, "{this} got unknown outgoing stream #{id} start request", ToString(), message.Id);
                return;
            }
            Logger?.Log(LogLevel.Debug, "{this} got outgoing stream #{id} start request", ToString(), message.Id);
            try
            {
                // Start streaming
                using Cancellations cancellations = new(CancelToken, stream.Cancellation.Token);
                await stream.StartAsync().DynamicContext();
                // Send the stream chunks
                using RentedArrayStructSimple<byte> buffer = new(RpcStreamValue.MaxContentLength, clean: false);
                int red,// Number of chunk bytes red
                    len;// Number of chunk bytes expected
                long total = 0;// Total number of bytes red
                bool isLastChunk = false;// If this is the last chunk
                while (!isLastChunk && EnsureUndisposed())
                {
                    // Read the chunk
                    len = await GetStreamChunkLengthAsync(stream, cancellations).DynamicContext();
                    if (len > buffer.Length)
                        throw new OutOfMemoryException($"Outgoing stream chunk length is larger than the buffer ({len}/{buffer.Length} bytes)");
                    red = await stream.ReadNextChunkAsync(buffer.Memory[..len], cancellations).DynamicContext();
                    if (red > len)
                        throw new InvalidProgramException($"Next outgoing stream chunk is larger than the buffer ({red}/{len} bytes)");
                    total += red;
                    if (stream.Value.Length.HasValue && total > stream.Value.Length.Value)
                        throw new IOException($"Total outgoing stream length is larger than expected ({total}/{stream.Value.Length.Value} bytes)");
                    isLastChunk = red != len || (stream.Value.Length.HasValue && total == stream.Value.Length.Value);
                    // Send the chunk and wait for the peer to confirm
                    Logger?.Log(LogLevel.Trace, "{this} sending outgoing stream #{id} chunk {len} bytes (last: {last})", ToString(), message.Id, red, isLastChunk);
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
                        }
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
                                Logger?.Log(LogLevel.Trace, "{this} outgoing stream #{id} waiting for chunk confirmation from the peer", ToString(), message.Id);
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
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (stream.RemoteCanceled || CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} sending outgoing stream #{id} chunks canceled", ToString(), message.Id);
            }
            catch (RpcRemoteException ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} sending outgoing stream #{id} chunks canceled due to a remote chunk handler error: {ex}", ToString(), message.Id, ex);
            }
            catch (Exception ex)
            {
                stream.LastException ??= ex;
                Logger?.Log(LogLevel.Warning, "{this} sending outgoing stream #{id} chunks canceled due to a local error: {ex}", ToString(), message.Id, ex);
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
                Logger?.Log(LogLevel.Debug, "{this} outgoing stream #{id} done within {runtime}", ToString(), message.Id, stream.Done - stream.Started);
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
            Logger?.Log(LogLevel.Debug, "{this} got outgoing stream #{id} close request", ToString(), message.Id);
            if (await GetOutgoingStreamAsync(message.Id!.Value, cancellationToken: CancelToken).DynamicContext() is OutgoingStream stream)
                try
                {
                    if (!stream.IsDisposing && !stream.Cancellation.IsCancellationRequested)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} set outgoing stream #{id} remote canceled", ToString(), message.Id);
                        stream.RemoteCanceled = true;
                        stream.Cancellation.Cancel();
                    }
                }
                catch
                {
                }
        }
    }
}
