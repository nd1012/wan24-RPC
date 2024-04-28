namespace wan24.RPC
{
    /// <summary>
    /// Attribute for an API version restricted RPC API method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RpcVersionAttribute : Attribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fromVersion">From version including</param>
        /// <param name="toVersion">To version including</param>
        /// <param name="newerMethodName">Newer method name (forwards the RPC processor to another RPC API method for newer API versions)</param>
        public RpcVersionAttribute(int fromVersion, int toVersion, string? newerMethodName = null) : base()
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            NewerMethodName = newerMethodName;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fromVersion">From version including</param>
        public RpcVersionAttribute(int fromVersion) : base() => FromVersion = fromVersion;

        /// <summary>
        /// From version including
        /// </summary>
        public int FromVersion { get; }

        /// <summary>
        /// To version including
        /// </summary>
        public int? ToVersion { get; }

        /// <summary>
        /// Newer RPC API method name
        /// </summary>
        public string? NewerMethodName { get; }

        /// <summary>
        /// Determine if an API version is compatible
        /// </summary>
        /// <param name="version">API version</param>
        /// <returns>If compatible</returns>
        public virtual bool IsCompatibleWithApiVersion(in int version)
        {
            if (version < FromVersion) return false;
            if (ToVersion.HasValue && version > ToVersion.Value) return false;
            return true;
        }

        /// <summary>
        /// Get the newer RPC API method name to use, if incompatible
        /// </summary>
        /// <param name="version">API version</param>
        /// <param name="currentMethodName">Current RPC API method name used</param>
        /// <returns>The current or newer RPC API method name to use, or <see langword="null"/>, if the method can't be served with the peers API version</returns>
        public virtual string? GetNewerMethodName(in int version, in string currentMethodName)
            => IsCompatibleWithApiVersion(version) ? currentMethodName : NewerMethodName;
    }
}
