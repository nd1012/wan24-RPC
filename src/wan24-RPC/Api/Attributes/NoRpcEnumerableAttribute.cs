namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for an enumerable RPC API method return value which shouldn't be RPC enumerated (will be transported as an array instead)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class NoRpcEnumerableAttribute() : Attribute()
    {
    }
}
