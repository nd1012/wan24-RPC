using wan24.RPC.Api.Attributes;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestUnauthorizedAttribute() : RpcAuthorizationAttributeBase()
    {
        public override Task<bool> IsContextAuthorizedAsync(RpcContext context) => Task.FromResult(false);
    }
}
