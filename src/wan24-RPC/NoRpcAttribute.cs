namespace wan24.RPC
{
    /// <summary>
    /// Attribute for a RPC API method (parameter) which isn't RPC servable
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcAttribute() : Attribute()
    {
    }
}
