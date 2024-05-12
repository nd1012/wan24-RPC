using wan24.Core;
using wan24.RPC.Processing.Values;

//TODO App configuration

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC stream (asynchronous operation ONLY!)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RpcStream() : BlockingBufferStream(RpcStreamValue.MaxContentLength)
    {
        /// <summary>
        /// Incoming stream
        /// </summary>
        protected IIncomingRpcStream? _IncomingStream = null;

        /// <summary>
        /// Incoming stream
        /// </summary>
        public IIncomingRpcStream IncomingStream
        {
            get => _IncomingStream ?? throw new InvalidOperationException();
            set
            {
                EnsureUndisposed();
                if (_IncomingStream is not null)
                    throw new InvalidOperationException();
                _IncomingStream = value;
            }
        }

        /// <summary>
        /// Leave this stream open when the <see cref="IncomingStream"/> is disposing?
        /// </summary>
        public bool LeaveOpen { get; set; }

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int ReadByte() => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int TryRead(Span<byte> buffer) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            if (!IncomingStream.IsStarted)
                await IncomingStream.StartAsync(cancellationToken).DynamicContext();
            EnsureValidIncomingStreamState();
            int res = await base.ReadAsync(buffer, cancellationToken).DynamicContext();
            EnsureValidIncomingStreamState();
            return res;
        }

        /// <inheritdoc/>
        public override async ValueTask<int> TryReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            if (!IncomingStream.IsStarted)
                await IncomingStream.StartAsync(cancellationToken).DynamicContext();
            EnsureValidIncomingStreamState();
            int res = await base.TryReadAsync(buffer, cancellationToken).DynamicContext();
            EnsureValidIncomingStreamState();
            return res;
        }

        /// <summary>
        /// Ensure a valid state
        /// </summary>
        protected virtual void EnsureValidIncomingStreamState()
        {
            if (IncomingStream.LastRemoteException is not null)
                throw new AggregateException(IncomingStream.LastRemoteException);
            if (IncomingStream.IsCanceled)
                throw new IOException("The RPC stream was canceled");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_IncomingStream is not null && !_IncomingStream.IsDisposed)
                _IncomingStream.CancelAsync().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (_IncomingStream is not null && !_IncomingStream.IsDisposed)
                await _IncomingStream.CancelAsync().DynamicContext();
            await base.DisposeCore().DynamicContext();
        }
    }
}
