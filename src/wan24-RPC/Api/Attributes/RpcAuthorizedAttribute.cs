namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for RPC API methods which doesn't require any authorization (while the RPC API type has a general <see cref="RpcAuthorizationAttributeBase"/>)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RpcAuthorizedAttribute() : Attribute()
    {
    }
}
