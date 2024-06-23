using Microsoft.Extensions.Logging;
using wan24.Core;

namespace wan24.RPC.Processing
{
    // Disposing
    public partial class RpcProcessor
    {
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Logger?.Log(LogLevel.Trace, "{this} sync disposing", ToString());
            RpcProcessorTable.Processors.TryRemove(GetHashCode(), out _);
            HeartBeat?.Dispose();
            PeerHeartBeat?.Dispose();
            ObjectDisposedException disposedException = new(GetType().ToString());
            // Stop processing
            base.Dispose(disposing);
            // Dispose message synchronization
            WriteSync.Dispose();
            // Dispose requests
            Requests.Dispose();
            foreach (Request request in PendingRequests.Values)
            {
                request.ProcessorCompletion.TrySetException(disposedException);
                request.RequestCompletion.TrySetException(disposedException);
            }
            PendingRequests.Values.DisposeAll();
            PendingRequests.Clear();
            // Dispose calls
            Calls.Dispose();
            foreach (Call call in PendingCalls.Values)
                call.Completion.TrySetException(disposedException);
            PendingCalls.Values.DisposeAll();
            PendingCalls.Clear();
            // Remove events
            _RemoteEvents.Clear();
            // Dispose remote scopes
            RemoteScopes.Values.DisposeAll();
            RemoteScopes.Clear();
            // Dispose scopes
            Scopes.Values.DisposeAll();
            Scopes.Clear();
            // Dispose incoming messages
            IncomingMessages.Dispose();
            // Dispose outgoing messages
            OutgoingMessages.Dispose();
            // Dispose others
            Options.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            Logger?.Log(LogLevel.Trace, "{this} async disposing", ToString());
            RpcProcessorTable.Processors.TryRemove(GetHashCode(), out _);
            if (HeartBeat is not null)
                await HeartBeat.DisposeAsync().DynamicContext();
            if (PeerHeartBeat is not null)
                await PeerHeartBeat.DisposeAsync().DynamicContext();
            ObjectDisposedException disposedException = new(GetType().ToString());
            // Stop processing
            await base.DisposeCore().DynamicContext();
            // Dispose message synchronization
            await WriteSync.DisposeAsync().DynamicContext();
            // Dispose requests
            await Requests.DisposeAsync().DynamicContext();
            foreach (Request request in PendingRequests.Values)
            {
                request.ProcessorCompletion.TrySetException(disposedException);
                request.RequestCompletion.TrySetException(disposedException);
            }
            await PendingRequests.Values.DisposeAllAsync().DynamicContext();
            PendingRequests.Clear();
            // Dispose calls
            await Calls.DisposeAsync().DynamicContext();
            foreach (Call call in PendingCalls.Values)
                call.Completion.TrySetException(disposedException);
            await PendingCalls.Values.DisposeAllAsync().DynamicContext();
            PendingCalls.Clear();
            // Remove events
            _RemoteEvents.Clear();
            // Dispose remote scopes
            await RemoteScopes.Values.DisposeAllAsync().DynamicContext();
            RemoteScopes.Clear();
            // Dispose scopes
            await Scopes.Values.DisposeAllAsync().DynamicContext();
            Scopes.Clear();
            // Dispose incoming messages
            await IncomingMessages.DisposeAsync().DynamicContext();
            // Dispose outgoing messages
            await OutgoingMessages.DisposeAsync().DynamicContext();
            // Dispose others
            await Options.DisposeAsync().DynamicContext();
        }
    }
}
