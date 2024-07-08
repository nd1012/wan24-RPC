namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for
    /// <list type="bullet">
    /// <item>A type which is not allowed to be deserialized (opt-out)</item>
    /// <item>A public non-RPC method of an API type</item>
    /// <item>A non-RPC servable RPC API method parameter, which must be given (by DI or from its default value)</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcAttribute() : Attribute()
    {
    }
}
