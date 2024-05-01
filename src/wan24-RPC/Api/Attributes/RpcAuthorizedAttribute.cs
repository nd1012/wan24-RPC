namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for RPC API methods which don't require any authorization
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RpcAuthorizedAttribute() : Attribute()
    {
    }
}
