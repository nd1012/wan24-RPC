namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for
    /// <list type="bullet">
    /// <item>A type which is not allowed to be deserialized</item>
    /// <item>A public non-RPC method of an API type</item>
    /// <item>A no-RPC servable RPC API method parameter</item>
    /// </list>
    /// (opt-out)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcAttribute() : Attribute()
    {
    }
}
