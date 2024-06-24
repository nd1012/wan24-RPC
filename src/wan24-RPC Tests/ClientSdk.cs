using wan24.RPC.Sdk;

namespace wan24_RPC_Tests
{
    public sealed class ClientSdk : RpcSdkBase<TestRpcProcessor>
    {
        public ClientSdk(TestRpcProcessor processor) : base(processor) => DisposeProcessor = false;

        public async Task<string> EchoAsync(string message)
        {
            EnsureInitialized();
            return await Processor.CallValueAsync<string>(nameof(ClientApi), nameof(ClientApi.EchoAsync), parameters: [message])
                ?? throw new InvalidDataException("NULL return value");
        }

        public async Task<string> EchoSyncAsync(string message)
        {
            EnsureInitialized();
            return await Processor.CallValueAsync<string>(nameof(ClientApi), nameof(ClientApi.Echo), parameters: [message])
                ?? throw new InvalidDataException("NULL return value");
        }
    }
}
