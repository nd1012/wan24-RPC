using wan24.Core;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

namespace wan24_RPC_Tests
{
    public sealed class TestRemoteScope(in TestRpcProcessor processor, in RpcScopeValue value) : RpcRemoteScope(processor, value)
    {
        public readonly TestDisposable _Value = new();

        public override int Type => TestScope.HL_TYPE;

        public override object? Value => _Value;

        new public static Task<RpcProcessor.RpcRemoteScopeBase> CreateAsync(RpcProcessor processor, RpcScopeValue value, CancellationToken cancellationToken = default)
        {
            Logging.WriteInfo($"Create remote scope {value.Id}");
            Logging.WriteInfo($"\tKey {value.Key}");
            Logging.WriteInfo($"\tReplace existing {value.ReplaceExistingScope}");
            Logging.WriteInfo($"\tDispose value {value.DisposeScopeValue}");
            Logging.WriteInfo($"\tDispose value on error {value.DisposeScopeValueOnError}");
            Logging.WriteInfo($"\tInform master {value.InformMasterWhenDisposing}");
            return Task.FromResult<RpcProcessor.RpcRemoteScopeBase>(new TestRemoteScope((TestRpcProcessor)processor, value));
        }
    }
}
