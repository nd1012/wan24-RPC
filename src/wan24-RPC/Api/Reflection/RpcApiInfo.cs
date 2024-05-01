using System.Collections.Frozen;
using System.Reflection;
using wan24.Core;
using wan24.RPC.Api.Attributes;

namespace wan24.RPC.Api.Reflection
{
    /// <summary>
    /// RPC API info
    /// </summary>
    public class RpcApiInfo : DisposableBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">API type</param>
        public RpcApiInfo(in Type type) : this(type.ConstructAuto()) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API instance</param>
        public RpcApiInfo(in object api) : base()
        {
            Type = api.GetType();
            Instance = api;
            Alias = Type.GetCustomAttributeCached<RpcAliasAttribute>();
            Authorization = Type.GetCustomAttributesCached<RpcAuthorizationAttributeBase>().ToFrozenSet();
            Version = Type.GetCustomAttributeCached<RpcVersionAttribute>();
            DisposeInstance = Type.GetCustomAttributeCached<NoRpcDisposeAttribute>() is null;
            Methods = new Dictionary<string, RpcApiMethodInfo>(
                from mi in Type.GetMethodsCached(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                where mi.GetCustomAttributeCached<NoRpcAttribute>() is null
                select new KeyValuePair<string, RpcApiMethodInfo>(mi.Name, new(this, mi))
                )
                .ToFrozenDictionary();
            if (Methods.Count < 1)
                throw new ArgumentException("No methods", nameof(api));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcApiInfo() : base() { }

        /// <summary>
        /// API type
        /// </summary>
        public Type Type { get; protected set; } = null!;

        /// <summary>
        /// API instance
        /// </summary>
        public object Instance { get; protected set; } = null!;

        /// <summary>
        /// Alias
        /// </summary>
        public RpcAliasAttribute? Alias { get; protected set; }

        /// <summary>
        /// Name prefix
        /// </summary>
        public string? NamePrefix { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public virtual string Name => $"{NamePrefix}{Alias?.Alias ?? Type.Name}";

        /// <summary>
        /// Authorization
        /// </summary>
        public FrozenSet<RpcAuthorizationAttributeBase> Authorization { get; protected set; } = null!;

        /// <summary>
        /// Version
        /// </summary>
        public RpcVersionAttribute? Version { get; protected set; }

        /// <summary>
        /// If to dispose the <see cref="Instance"/> after use
        /// </summary>
        public bool DisposeInstance { get; protected set; }

        /// <summary>
        /// Methods
        /// </summary>
        public FrozenDictionary<string, RpcApiMethodInfo> Methods { get; protected set; } = null!;

        /// <summary>
        /// Find a method by its name
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>Method</returns>
        public RpcApiMethodInfo? FindMethod(string name)
            => Methods.TryGetValue(name, out RpcApiMethodInfo? res)
                ? res
                : Methods.Values.FirstOrDefault(m => m.Name == name);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (DisposeInstance)
                Instance.TryDispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (DisposeInstance)
                await Instance.TryDisposeAsync().DynamicContext();
        }
    }
}
