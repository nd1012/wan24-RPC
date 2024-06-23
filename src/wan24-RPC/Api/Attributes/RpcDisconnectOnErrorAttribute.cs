namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for APIs or API methods which need the RPC processor to disconnect on any API method execution error
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RpcDisconnectOnErrorAttribute() : Attribute()
    {
    }
}
