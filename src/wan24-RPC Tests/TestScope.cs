using wan24.Core;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;

namespace wan24_RPC_Tests
{
    public sealed class TestScope(in TestRpcProcessor processor, in string? key = null) : RpcScope(processor, key)
    {
        public const int HL_TYPE = 1234;

        public override int Type => HL_TYPE;

        new public static Task<RpcProcessor.RpcScopeBase> CreateAsync(RpcProcessor processor, IRpcScopeParameter parameter, CancellationToken cancellationToken = default)
        {
            Logging.WriteInfo($"Create local scope");
            Logging.WriteInfo($"\tKey {parameter.Key}");
            Logging.WriteInfo($"\tReplace existing {parameter.ReplaceExistingScope}");
            Logging.WriteInfo($"\tDispose value {parameter.DisposeScopeValue}");
            Logging.WriteInfo($"\tDispose value on error {parameter.DisposeScopeValueOnError}");
            Logging.WriteInfo($"\tInform master {parameter.InformMasterWhenDisposing}");
            return Task.FromResult<RpcProcessor.RpcScopeBase>(new TestScope((TestRpcProcessor)processor, parameter.Key)
            {
                ScopeParameter = parameter
            });
        }
    }
}
