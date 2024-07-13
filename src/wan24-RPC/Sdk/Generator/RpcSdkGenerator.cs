#if !RELEASE //TODO Enable when fully implemented
using System.Reflection;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection;

namespace wan24.RPC.Sdk.Generator
{
    /// <summary>
    /// RPC SDK generator (won't create DTOs!)
    /// </summary>
    public static partial class RpcSdkGenerator
    {
        /// <summary>
        /// Find RPC APIs in assemblies
        /// </summary>
        /// <param name="assemblies">Assemblies to scan for RPC API types with a <see cref="GenerateSdkAttribute"/> (if not given, <see cref="TypeHelper.Assemblies"/> 
        /// from <see cref="TypeHelper.Instance"/> will be used)</param>
        /// <returns>API</returns>
        public static Dictionary<string, RpcApiInfo> FindApis(params Assembly[] assemblies) => FindApis(sdk: null, assemblies);

        /// <summary>
        /// Find RPC APIs in assemblies
        /// </summary>
        /// <param name="sdk">SDK type name (f.e. <c>YourSdk</c>)</param>
        /// <param name="assemblies">Assemblies to scan for RPC API types with a <see cref="GenerateSdkAttribute"/> (if not given, <see cref="TypeHelper.Assemblies"/> 
        /// from <see cref="TypeHelper.Instance"/> will be used)</param>
        /// <returns>API</returns>
        public static Dictionary<string, RpcApiInfo> FindApis(string? sdk, params Assembly[] assemblies)
        {
            Dictionary<string, RpcApiInfo> res = [];
            if (assemblies.Length == 0)
                assemblies = TypeHelper.Instance.Assemblies;
            foreach (RpcApiInfo api in from assembly in assemblies
                                       from type in assembly.GetTypes()
                                       where type.CanConstruct() &&
                                        type.GetCustomAttributeCached<GenerateSdkAttribute>() is GenerateSdkAttribute attr &&
                                        attr.Sdk == sdk
                                       select new RpcApiInfo(type))
                res[api.Name] = api;
            return res;
        }
    }
}
#endif
