using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Values;

/*
 * The usual asynchronous streaming process is:
 * 
 * 1. The RPC processor received a stream parameter or return value
 * 2. The RPC processor stores an incoming stream and uses a RPC stream instead
 * 3. As soon as the consuming code starts reading from the RPC stream, the RPC processor will send a stream start message to the peer
 * 4. Received stream chunks will be written to the RPC stream for reading from the consuming code
 * 5. The last chunk sets the RPC stream to an EOF (end of file) state, and the RPC processor removes the incoming stream
 * 
 * A stream parameter will be disposed from the RPC processor after the consuming code returned (the NoRpcDisposeAttribute has no effect here!). The consuming code has 
 * to complete streaming before it returns (copy the stream into a temporary stream to make it available to other code after the method returns). For early canceling a 
 * stream the consuming code may send a remote close message to the peer by simply disposing the RPC stream. The peer may send a local close message, which will set the 
 * RPC stream to an error state.
 * 
 * If an API method returns a stream, it'll be used from the RPC processor asynchronous and disposed after the sending process finished. Code outside shouldn't dispose the 
 * stream, or the streaming may fail random. If the returned stream shouldn't be disposed from the RPC processor, the NoRpcDisposeAttribute must be applied to the method.
 * 
 * The number of incoming streams is limited to protect memory ressources. It should be equal to the max. number of outgoing streams at the peer.
 */

namespace wan24.RPC.Processing
{
    // Incoming stream
    public partial class RpcProcessor
    {
        /// <summary>
        /// Incoming streams thread synchronization
        /// </summary>
        protected readonly SemaphoreSync IncomingStreamsSync = new()
        {
            Name = "Incoming RPC stream synchronization"
        };
        /// <summary>
        /// Incoming streams (key is the stream ID)
        /// </summary>
        protected readonly Dictionary<long, IncomingStream> IncomingStreams = [];

        /// <summary>
        /// Add an incoming stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>If added</returns>
        protected virtual bool AddIncomingStream(in IncomingStream stream, in bool lockDict = false)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            if (!stream.Value.Stream.HasValue)
                throw new ArgumentException("Missing stream ID", nameof(stream));
            using SemaphoreSyncContext? ssc = lockDict ? IncomingStreamsSync.SyncContext() : null;
            EnsureUndisposed();
            if (IncomingStreams.Count > Options.MaxStreamCount)
                throw new TooManyRpcStreamsException("Maximum number of incoming streams exceeded");
            return IncomingStreams.TryAdd(stream.Value.Stream.Value, stream);
        }

        /// <summary>
        /// Add an incoming stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>If added</returns>
        protected virtual async Task<bool> AddIncomingStreamAsync(IncomingStream stream, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            if (!stream.Value.Stream.HasValue)
                throw new ArgumentException("Missing stream ID", nameof(stream));
            using SemaphoreSyncContext? ssc = lockDict ? await IncomingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed();
            if (IncomingStreams.Count > Options.MaxStreamCount)
                throw new TooManyRpcStreamsException("Maximum number of outgoing streams exceeded");
            return IncomingStreams.TryAdd(stream.Value.Stream.Value, stream);
        }

        /// <summary>
        /// Get an incoming stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>Stream</returns>
        protected virtual IncomingStream? GetIncomingStream(in long id, in bool lockDict = false)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? IncomingStreamsSync.SyncContext() : null;
            EnsureUndisposed();
            return IncomingStreams.TryGetValue(id, out IncomingStream? res) ? res : null;
        }

        /// <summary>
        /// Get an incoming stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream</returns>
        protected virtual async Task<IncomingStream?> GetIncomingStreamAsync(long id, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? await IncomingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed();
            return IncomingStreams.TryGetValue(id, out IncomingStream? res) ? res : null;
        }

        /// <summary>
        /// Remove an incoming stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        protected virtual void RemoveIncomingStream(in IncomingStream stream, in bool lockDict = false)
        {
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? IncomingStreamsSync.SyncContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!stream.Value.Stream.HasValue)
                throw new ArgumentException("Missing stream ID", nameof(stream));
            IncomingStreams.Remove(stream.Value.Stream.Value);
        }

        /// <summary>
        /// Remove an incoming stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <returns>Stream (don't forget to dispose!)</returns>
        protected virtual IncomingStream? RemoveIncomingStream(in long id, in bool lockDict = false)
        {
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? IncomingStreamsSync.SyncContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!IncomingStreams.TryGetValue(id, out IncomingStream? res))
                return null;
            IncomingStreams.Remove(id);
            return res;
        }

        /// <summary>
        /// Remove an incoming stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task RemoveIncomingStreamAsync(IncomingStream stream, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? await IncomingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!stream.Value.Stream.HasValue)
                throw new ArgumentException("Missing stream ID", nameof(stream));
            IncomingStreams.Remove(stream.Value.Stream.Value);
        }

        /// <summary>
        /// Remove an incoming stream
        /// </summary>
        /// <param name="id">Stream ID</param>
        /// <param name="lockDict">If to lock the dictionary</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream (don't forget to dispose!)</returns>
        protected virtual async Task<IncomingStream?> RemoveIncomingStreamAsync(long id, bool lockDict = true, CancellationToken cancellationToken = default)
        {
            EnsureStreamsAreEnabled();
            using SemaphoreSyncContext? ssc = lockDict ? await IncomingStreamsSync.SyncContextAsync(cancellationToken).DynamicContext() : null;
            EnsureUndisposed(allowDisposing: true);
            if (!IncomingStreams.TryGetValue(id, out IncomingStream? res))
                return null;
            IncomingStreams.Remove(id);
            return res;
        }

        /// <summary>
        /// Create an incoming stream
        /// </summary>
        /// <param name="stream">Received stream value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream (don't forget to dispose!)</returns>
        public virtual async Task<Stream> CreateIncomingStreamAsync(RpcStreamValue stream, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureStreamsAreEnabled();
            if (stream.Content is not null)
                return new MemoryStream(stream.Content);
            IncomingStream res = new()
            {
                Processor = this,
                Value = stream,
                Stream = new()
            };
            res.Stream.IncomingStream = res;
            try
            {
                if (await AddIncomingStreamAsync(res, cancellationToken: cancellationToken).DynamicContext())
                    return new ForceAsyncStream(res.Stream);
            }
            catch(Exception ex)
            {
                await HandleIncomingStreamProcessingErrorAsync(res, ex).DynamicContext();
                throw;
            }
            InvalidDataException exception = new($"Failed to store incoming stream #{stream.Stream} (double incoming stream ID)");
            await HandleIncomingStreamProcessingErrorAsync(res, exception).DynamicContext();
            await res.DisposeAsync().DynamicContext();
            throw exception;
        }

        /// <summary>
        /// Handle an incoming stream close message (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleLocalStreamCloseAsync(LocalStreamCloseMessage message)
        {
            EnsureStreamsAreEnabled();
            Logger?.Log(LogLevel.Debug, "{this} got incoming stream #{id} close request", ToString(), message.Id);
            if (await RemoveIncomingStreamAsync(message.Id!.Value, cancellationToken: CancelToken).DynamicContext() is IncomingStream stream)
                try
                {
                    await stream.SetRemoteExceptionAsync(message).DynamicContext();
                }
                catch (ObjectDisposedException) when (IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    await HandleIncomingStreamProcessingErrorAsync(stream, ex).DynamicContext();
                    throw;
                }
                finally
                {
                    await stream.DisposeAsync().DynamicContext();
                }
        }

        /// <summary>
        /// Handle an incoming stream chunk (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleStreamChunkAsync(StreamChunkMessage message)
        {
            EnsureStreamsAreEnabled();
            Logger?.Log(LogLevel.Trace, "{this} got incoming stream #{stream} chunk message #{id}", ToString(), message.Stream, message.Id);
            if (await GetIncomingStreamAsync(message.Stream, cancellationToken: CancelToken).DynamicContext() is not IncomingStream stream)
            {
                Logger?.Log(LogLevel.Debug, "{this} incoming stream #{stream} chunk message #{id} discarded (stream not found)", ToString(), message.Stream, message.Id);
                return;
            }
            try
            {
                if (!stream.IsStarted)
                {
                    Logger?.Log(LogLevel.Warning, "{this} incoming stream #{stream} chunk message #{id} for unprepared stream", ToString(), message.Stream, message.Id);
                    throw new InvalidOperationException("Incoming stream wasn't prepared for received chunk data yet");
                }
                if (!stream.IsChunkRequested)
                {
                    Logger?.Log(LogLevel.Warning, "{this} incoming stream #{stream} chunk message #{id} without request", ToString(), message.Stream, message.Id);
                    throw new InvalidOperationException("Incoming stream chunk data without request received");
                }
                stream.IsChunkRequested = false;
                if (stream.IsDone)
                {
                    Logger?.Log(LogLevel.Warning, "{this} incoming stream #{stream} chunk message #{id} for finalized stream", ToString(), message.Stream, message.Id);
                    throw new InvalidOperationException("Incoming stream chunk data received for an already finalized stream");
                }
                if (message.Data is not null)
                {
                    Logger?.Log(LogLevel.Trace, "{this} incoming stream #{stream} chunk message #{id} contains {len} bytes", ToString(), message.Stream, message.Id, message.Data.Length);
                    if (message.Data.Length > RpcStreamValue.MaxContentLength)
                        throw new InvalidDataException($"Max. incoming stream chunk length exceeded ({message.Data.Length}/{RpcStreamValue.MaxContentLength} bytes)");
                    await stream.ChunkTarget.WriteAsync(message.Data, CancelToken).DynamicContext();
                }
                if (message.IsLastChunk)
                {
                    Logger?.Log(LogLevel.Debug, "{this} incoming stream #{stream} chunk message #{id} finalizes the stream", ToString(), message.Stream, message.Id);
                    await stream.CompleteAsync().DynamicContext();
                    await RemoveIncomingStreamAsync(stream, cancellationToken: CancellationToken.None).DynamicContext();
                    await stream.DisposeAsync().DynamicContext();
                }
                else
                {
                    Logger?.Log(LogLevel.Trace, "{this} incoming stream #{stream} requires more data after processing chunk message #{id}", ToString(), message.Stream, message.Id);
                    stream.IsChunkRequested = true;
                    await SendMessageAsync(new ResponseMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = message.Id
                    }, CancelToken).DynamicContext();
                }
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await HandleIncomingStreamProcessingErrorAsync(stream, ex).DynamicContext();
                throw;
            }
        }
    }
}
