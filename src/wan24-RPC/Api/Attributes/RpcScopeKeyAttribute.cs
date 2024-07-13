namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// RPC scope key attribute to tell the call processor to get the RPC API method parameter value from a local scope which was stored using the specified key 
    /// (the scope won't be disposed)
    /// </summary>
    /// <param name="key">Key</param>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcScopeKeyAttribute(string key) : Attribute(), INoRpcDisposeAttribute
    {
        /// <summary>
        /// Key
        /// </summary>
        public string Key { get; } = key;

        /// <summary>
        /// Throw an exception when the scope wasn't found?
        /// </summary>
        public bool ThrowOnMissingScope { get; set; }
    }
}
