namespace wan24.RPC
{
    /// <summary>
    /// Attribute for a RPC API method stream return value or parameter which shouldn't be communicated compressed
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcCompressionAttribute() : Attribute()
    {
    }
}
