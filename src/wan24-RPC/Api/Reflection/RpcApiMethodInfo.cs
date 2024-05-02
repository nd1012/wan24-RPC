using System.Collections.Frozen;
using System.Reflection;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection.Extensions;

namespace wan24.RPC.Api.Reflection
{
    /// <summary>
    /// RPC API method info
    /// </summary>
    public class RpcApiMethodInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API</param>
        /// <param name="method">Method</param>
        public RpcApiMethodInfo(RpcApiInfo api, MethodInfo method)
        {
            if (method.GetCustomAttributeCached<NoRpcAttribute>() is not null)
                throw new ArgumentException("Not a RPC method", nameof(method));
            API = api;
            Method = method;
            Alias = method.GetCustomAttributeCached<RpcAliasAttribute>();
            Authorization = method.GetCustomAttributesCached<RpcAuthorizationAttributeBase>().ToFrozenSet();
            Authorize = method.GetCustomAttribute<RpcAuthorizedAttribute>() is not null;
            EnumerateReturnValue = method.ReturnType.IsEnumerable() && method.GetCustomAttributeCached<NoRpcEnumerableAttribute>() is null;
            Stream = method.GetCustomAttributeCached<RpcStreamAttribute>();
            Version = method.GetCustomAttributeCached<RpcVersionAttribute>();
            DisposeReturnValue = method.GetCustomAttributeCached<NoRpcDisposeAttribute>() is null;
            int index = -1;
            NullabilityInfoContext nic = new();
            Parameters = new Dictionary<string, RpcApiMethodParameterInfo>(
                from pi in method.GetParametersCached()
                select new KeyValuePair<string, RpcApiMethodParameterInfo>(
                    pi.Name ?? throw new InvalidProgramException($"Missing parameter name (method \"{api.Type.Name}.{method.Name}\" at index #{++index})"), 
                    new(this, pi, ++index, nic)
                    )
                )
                .ToFrozenDictionary();
            RpcParameters = new HashSet<RpcApiMethodParameterInfo>(Parameters.Values.Where(p => p.RPC)).ToFrozenSet();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcApiMethodInfo() { }

        /// <summary>
        /// API
        /// </summary>
        public RpcApiInfo API { get; protected set; } = null!;

        /// <summary>
        /// Method
        /// </summary>
        public MethodInfo Method { get; protected set; } = null!;

        /// <summary>
        /// Alias
        /// </summary>
        public RpcAliasAttribute? Alias { get; protected set; }

        /// <summary>
        /// Name
        /// </summary>
        public virtual string Name => Alias?.Alias ?? Method.Name;

        /// <summary>
        /// Authorization
        /// </summary>
        public FrozenSet<RpcAuthorizationAttributeBase> Authorization { get; protected set; } = null!;

        /// <summary>
        /// If authorized for every context
        /// </summary>
        public bool Authorize { get; protected set; }

        /// <summary>
        /// If to enumerate the return value, if applicable
        /// </summary>
        public bool EnumerateReturnValue { get; protected set; } = true;

        /// <summary>
        /// If to dispose the return value after sending
        /// </summary>
        public bool DisposeReturnValue { get; protected set; } = true;

        /// <summary>
        /// Stream configuration
        /// </summary>
        public RpcStreamAttribute? Stream { get; protected set; }

        /// <summary>
        /// Version
        /// </summary>
        public RpcVersionAttribute? Version { get; protected set; }

        /// <summary>
        /// Parameters
        /// </summary>
        public FrozenDictionary<string, RpcApiMethodParameterInfo> Parameters { get; protected set; } = null!;

        /// <summary>
        /// RPC parameters
        /// </summary>
        public FrozenSet<RpcApiMethodParameterInfo> RpcParameters { get; protected set; } = null!;

        /// <inheritdoc/>
        public override string ToString() => $"{API.Name}->{Name} ({API.Type.GetType()}.{Method.Name})";
    }
}
