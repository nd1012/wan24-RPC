using wan24.RPC.Api;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    [NoRpcDispose]
    public sealed class ServerApi() : DisposableRpcApiBase(asyncDisposing: false), IWantRpcProcessorInfo
    {
        public TestDisposable? ClientObj = null;
        public TestDisposable? ServerObj = null;

        public RpcProcessor? Processor { get; set; }

        public CancellationToken ProcessorCancellation { get; set; }

        public Task<string> EchoAsync(string message, [NoRpc] RpcProcessor processor, [NoRpc] CancellationToken cancellationToken)
        {
            Assert.IsNotNull(processor);
            Assert.IsFalse(Equals(default, cancellationToken));
            Assert.IsFalse(Equals(ProcessorCancellation, cancellationToken));
            return Task.FromResult(message);
        }

        public string Echo(string message, [NoRpc] RpcProcessor processor, [NoRpc] CancellationToken cancellationToken)
        {
            Assert.IsNotNull(processor);
            Assert.IsFalse(Equals(default, cancellationToken));
            Assert.IsFalse(Equals(ProcessorCancellation, cancellationToken));
            return message;
        }

        public Task RaiseRemoteEventAsync([NoRpc] RpcProcessor processor) => processor.RaiseEventAsync("test", wait: true);

        public TestDisposable Scopes(TestDisposable obj)
        {
            Assert.IsNotNull(obj);
            obj.Name ??= "Remote client";
            ClientObj = obj;// Disposed after the method returned
            ServerObj = new()
            {
                Name = "Server"
            };
            return ServerObj;// Disposed after the return value was sent to the peer
        }

        protected override void Dispose(bool disposing)
        {
            ClientObj?.Dispose();
            ServerObj?.Dispose();
        }
    }
}
