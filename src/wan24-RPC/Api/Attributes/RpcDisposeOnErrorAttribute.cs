namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for
    /// <list type="bullet">
    /// <item>A disposable RPC API method parameter which should be disposed on error</item>
    /// <item>A disposable RPC API method return value which should be disposed on error</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class RpcDisposeOnErrorAttribute() : Attribute()
    {
    }
}
