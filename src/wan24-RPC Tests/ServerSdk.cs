using wan24.RPC.Sdk;

namespace wan24_RPC_Tests
{
    public sealed class ServerSdk : RpcSdkBase
    {
        public ServerSdk(TestRpcProcessor processor) : base(processor) => DisposeProcessor = false;

        public async Task<string> EchoAsync(string message)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<string>(nameof(ServerApi), nameof(ServerApi.EchoAsync), parameters: [message])
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task<string> EchoSyncAsync(string message)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<string>(nameof(ServerApi), nameof(ServerApi.Echo), parameters: [message])
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task RaiseRemoteEventAsync()
        {
            EnsureUndisposed();
            EnsureInitialized();
            await Processor.CallVoidAsync(nameof(ServerApi), nameof(ServerApi.RaiseRemoteEventAsync));
        }

        public async Task<TestDisposable> ScopesAsync(TestDisposable obj)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<TestDisposable>(nameof(ServerApi), nameof(ServerApi.Scopes), default, obj)
                ?? throw new InvalidDataException("NULL return value");
        }
    }
}
