using wan24.Core;
using wan24.RPC.Api;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    [NoRpcDispose]
    public sealed class ServerApi() : DisposableRpcApiBase(asyncDisposing: false)
    {
        private readonly SemaphoreSync Sync = new()
        {
            Name = "RPC tests server API synchronization"
        };
        public readonly ResetEvent ResetEvent = new();

        public Task<string> EchoAsync(string message) => Task.FromResult(message);

        public string Echo(string message) => message;

        public Task RaiseRemoteEventAsync([NoRpc] RpcProcessor processor) => processor.RaiseEventAsync("test", wait: true);

        public async Task BlockResetEventAsync([NoRpc] CancellationToken cancellationToken)
        {
            using SemaphoreSyncContext ssc = await Sync.SyncContextAsync(cancellationToken);
            await ResetEvent.WaitAndResetAsync(cancellationToken);
        }

        public void SetResetEvent() => ResetEvent.Set();

        protected override void Dispose(bool disposing)
        {
            ResetEvent.Dispose();
            Sync.Dispose();
        }
    }
}
