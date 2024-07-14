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

        public async Task NotAuthorizedAsync(CancellationToken cancellationToken)
        {
            EnsureUndisposed();
            EnsureInitialized();
            await Processor.CallVoidAsync(nameof(ServerApi), nameof(ServerApi.NotAuthorized), cancellationToken);
        }

        public async Task<TestDisposable> ScopesAsync(TestDisposable obj)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<TestDisposable>(nameof(ServerApi), nameof(ServerApi.Scopes), default, obj)
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task<TestRemoteScope> Scopes2Async(TestDisposable obj)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<TestRemoteScope>(nameof(ServerApi), nameof(ServerApi.Scopes2), default, obj)
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task<TestRemoteScope> Scopes3Async(TestDisposable obj)
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<TestRemoteScope>(nameof(ServerApi), nameof(ServerApi.Scopes3), default, new TestScopeParameter()
            {
                ScopeObject = obj,
                DisposeScopeValue = false,
                DisposeScopeValueOnError = true
            })
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task CancellationParameterAsync(CancellationToken cancellationToken)
        {
            EnsureUndisposed();
            EnsureInitialized();
            await Processor.CallVoidAsync(nameof(ServerApi), nameof(ServerApi.CancellationParameterAsync), default, [cancellationToken]);
        }

        public async Task<CancellationToken> CancellationReturnAsync()
        {
            EnsureUndisposed();
            EnsureInitialized();
            return await Processor.CallValueAsync<CancellationToken>(nameof(ServerApi), nameof(ServerApi.CancellationReturn));
        }
    }
}
