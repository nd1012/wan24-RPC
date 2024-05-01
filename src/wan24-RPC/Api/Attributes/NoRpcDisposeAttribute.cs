namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for
    /// <list type="bullet">
    /// <item>A disposable RPC API method parameter which shouldn't be disposed after the method returned regular</item>
    /// <item>A disposable RPC API method return value which shouldn't be disposed after the response was sent to the peer</item>
    /// <item>A disposable RPC API type which shouldn't be disposed when the RPC processor is disposing</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcDisposeAttribute() : Attribute()
    {
    }
}
