namespace wan24.RPC
{
    /// <summary>
    /// Attribute for a RPC API (method) which uses an alias name
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="alias">Alias name</param>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RpcAliasAttribute(string alias) : Attribute()
    {
        /// <summary>
        /// Alias name
        /// </summary>
        public virtual string Alias { get; } = alias;
    }
}
