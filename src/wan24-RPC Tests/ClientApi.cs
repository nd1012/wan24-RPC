using wan24.RPC.Api;

namespace wan24_RPC_Tests
{
    public sealed class ClientApi() : RpcApiBase()
    {
        public Task<string> EchoAsync(string message) => Task.FromResult(message);

        public string Echo(string message) => message;
    }
}
