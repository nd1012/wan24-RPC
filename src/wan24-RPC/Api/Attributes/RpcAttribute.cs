namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for a RPC type which is allowed to be deserialized (opt-in)
    /// <list type="bullet">
    /// <item>a RPC type which is allowed to be deserialized (opt-in)</item>
    /// <item>a RPC API method parameter which is required to be given from the requesting peer</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
    public class RpcAttribute() : Attribute()
    {
    }
}
