using wan24.Core;

namespace wan24.RPC.Api.Reflection
{
    /// <summary>
    /// Reflection extensions
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Find an API by its name
        /// </summary>
        /// <param name="apis">APIs</param>
        /// <param name="name">Name</param>
        /// <returns>API</returns>
        public static RpcApiInfo? FindApi(this IReadOnlyDictionary<string, RpcApiInfo> apis, string name)
            => apis.TryGetValue(name, out RpcApiInfo? res)
                ? res
                : apis.Values.FirstOrDefault(a => a.Name == name);

        /// <summary>
        /// Find an API method by its name
        /// </summary>
        /// <param name="apis">APIs</param>
        /// <param name="name">Name</param>
        /// <returns>API method</returns>
        public static RpcApiMethodInfo? FindApiMethod(this IReadOnlyDictionary<string, RpcApiInfo> apis, in string name)
        {
            foreach (RpcApiInfo api in apis.Values)
                if (api.FindMethod(name) is RpcApiMethodInfo res)
                    return res;
            return null;
        }

        /// <summary>
        /// Determine if a type is an <see cref="IEnumerable{T}"/> or <see cref="IAsyncEnumerable{T}"/>
        /// </summary>
        /// <param name="type">Type</param>
        /// <param name="strict">If the type must be strictly an <see cref="IEnumerable{T}"/> or <see cref="IAsyncEnumerable{T}"/> (may be the generic type 
        /// definition also)</param>
        /// <param name="asyncOnly"><see cref="IAsyncEnumerable{T}"/> only?</param>
        /// <returns>If the type is an enumerable</returns>
        public static bool IsEnumerable(this Type type, in bool strict = false, bool asyncOnly = false)
        {
            if (!strict)
                return type.GetInterfaces().Any(i => i.IsEnumerable(strict: true, asyncOnly));
            if (!type.IsInterface || !type.IsGenericType)
                return false;
            Type gtd = type.EnsureGenericTypeDefinition();
            return gtd == typeof(IAsyncEnumerable<>) || !asyncOnly && gtd == typeof(IEnumerable<>);
        }
    }
}
