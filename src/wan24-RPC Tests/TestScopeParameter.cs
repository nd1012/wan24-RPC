using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Parameters;

namespace wan24_RPC_Tests
{
    public sealed record class TestScopeParameter : RpcScopeParameter
    {
        [SetsRequiredMembers]
        public TestScopeParameter() : base(TestScope.HL_TYPE) { }

        new public static Task<IRpcScopeParameter> CreateAsync(
            RpcProcessor processor,
            RpcProcessor.RpcScopeBase scope,
            RpcApiMethodInfo? apiMethod = null,
            CancellationToken cancellationToken = default
            )
        {
            TestScopeParameter parameter = new()
            {
                Processor = processor,
                ScopeObject = new TestDisposable()
            };
            Logging.WriteInfo($"Create local scope parameter");
            Logging.WriteInfo($"\tKey {parameter.Key}");
            Logging.WriteInfo($"\tReplace existing {parameter.ReplaceExistingScope}");
            Logging.WriteInfo($"\tDispose value {parameter.DisposeScopeValue}");
            Logging.WriteInfo($"\tDispose value on error {parameter.DisposeScopeValueOnError}");
            Logging.WriteInfo($"\tInform master {parameter.InformMasterWhenDisposing}");
            return Task.FromResult<IRpcScopeParameter>(parameter);
        }
    }
}
