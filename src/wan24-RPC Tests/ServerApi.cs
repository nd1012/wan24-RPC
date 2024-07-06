﻿using wan24.RPC.Api;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;

namespace wan24_RPC_Tests
{
    [NoRpcDispose]
    public sealed class ServerApi() : DisposableRpcApiBase(asyncDisposing: false), IWantRpcProcessorInfo
    {
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

        public RpcScope ReturnScope([NoRpc] RpcProcessor processor) => new(processor);

        public RpcScopeParameter ReturnScopeParameter() => new();

        protected override void Dispose(bool disposing) { }
    }
}
