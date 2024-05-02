using wan24.RPC.Api.Reflection;

namespace wan24.RPC.Api.Attributes
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
        /// Newer RPC API method name to use
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
        /// <param name="currentMethod">Current RPC API method used</param>
        /// <returns>The current or newer RPC API method name to use, or <see langword="null"/>, if the method can't be served with the peers API version</returns>
        public virtual string? GetNewerMethodName(in int version, in RpcApiMethodInfo currentMethod)
            => IsCompatibleWithApiVersion(version) ? currentMethod.Name : NewerMethodName;

        /// <inheritdoc/>
        public override string ToString()
            => $"#{FromVersion} to #{ToVersion?.ToString() ?? "n"}{(NewerMethodName is null ? string.Empty : $", otherwise \"{NewerMethodName}\"")}";
    }
}
