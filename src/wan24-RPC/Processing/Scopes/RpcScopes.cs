using wan24.Core;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope factories
    /// </summary>
    public static class RpcScopes
    {
        /// <summary>
        /// Registered scope factories (key is the scope type ID (see <see cref="RpcScopeTypes"/>))
        /// </summary>
        public static readonly Dictionary<int, ScopeFactory_Delegate> Factories = [];
        /// <summary>
        /// Registered remote scope factories (key is the scope type ID (see <see cref="RpcScopeTypes"/>))
        /// </summary>
        public static readonly Dictionary<int, RemoteScopeFactory_Delegate> RemoteFactories = [];
        /// <summary>
        /// Registered parameter scope factories (key is the parameter value type)
        /// </summary>
        public static readonly Dictionary<Type, ParameterScopeFactory_Delegate> ParameterScopeFactories = [];
        /// <summary>
        /// Registered return scope factories (key is the return value type)
        /// </summary>
        public static readonly Dictionary<Type, ReturnScopeFactory_Delegate> ReturnScopeFactories = [];

        /// <summary>
        /// Constructor
        /// </summary>
        static RpcScopes()
        {
            // Simple scope
            Factories[(int)RpcScopeTypes.Scope] = (processor, parameter, ct) =>
            {
                if (parameter is not RpcScopeParameter)
                    throw new InvalidDataException("Invalid parameter type for the requested scope type");
                return Task.FromResult<RpcProcessor.RpcScopeBase>(new RpcScope(processor, parameter.Key)
                {
                    ScopeParameter = parameter
                });
            };
            RemoteFactories[(int)RpcScopeTypes.Scope] = (processor, value, ct) => Task.FromResult<RpcProcessor.RpcRemoteScopeBase>(new RpcRemoteScope(processor, value));
            //TODO Register more built-in scope factories
        }

        /// <summary>
        /// Register a custom scope
        /// </summary>
        /// <param name="scopeType">Scope type (see <see cref="RpcScopeTypes"/>)</param>
        /// <param name="scopeFactory">Scope factory</param>
        /// <param name="remoteScopeFactory">Remote scope factory</param>
        /// <param name="parameterType">Parameter type</param>
        /// <param name="parameterScopeFactory">Parameter scope factory</param>
        /// <param name="returnType">Return type</param>
        /// <param name="returnScopeFactory">Return scope factory</param>
        /// <exception cref="ArgumentNullException">Both parameter/return type informations must be given</exception>
        /// <exception cref="InvalidOperationException">Won't overwrite existing scope registration</exception>
        public static void RegisterScope(
            int scopeType, 
            ScopeFactory_Delegate scopeFactory, 
            RemoteScopeFactory_Delegate remoteScopeFactory,
            Type? parameterType = null,
            ParameterScopeFactory_Delegate? parameterScopeFactory = null,
            Type? returnType = null,
            ReturnScopeFactory_Delegate? returnScopeFactory = null
            )
        {
            if (parameterType is null != parameterScopeFactory is null)
                throw new ArgumentNullException("Incomplete parameter type scope factory informations", innerException: null);
            if (returnType is null != returnScopeFactory is null)
                throw new ArgumentNullException("Incomplete return type scope factory informations", innerException: null);
            if (Factories.ContainsKey(scopeType))
                throw new InvalidOperationException($"Scope type #{scopeType} exists already");
            if (parameterType is not null && ParameterScopeFactories.ContainsKey(parameterType))
                throw new InvalidOperationException($"Parameter type {parameterType} exists already");
            if (returnType is not null && ReturnScopeFactories.ContainsKey(returnType))
                throw new InvalidOperationException($"Return type {returnType} exists already");
            Factories[scopeType] = scopeFactory;
            RemoteFactories[scopeType] = remoteScopeFactory;
            if (parameterScopeFactory is not null) ParameterScopeFactories[parameterType!] = parameterScopeFactory;
            if (returnScopeFactory is not null) ReturnScopeFactories[returnType!] = returnScopeFactory;
        }

        /// <summary>
        /// Get the parameter scope factory for a parameter value type
        /// </summary>
        /// <param name="type">Parameter value type</param>
        /// <returns>Parameter scope factory</returns>
        public static ParameterScopeFactory_Delegate? GetParameterScopeFactory(Type type)
            => ParameterScopeFactories.Keys.GetClosestType(type) is Type factoryType && 
                ParameterScopeFactories.TryGetValue(factoryType, out ParameterScopeFactory_Delegate? res) 
                ? res 
                : null;

        /// <summary>
        /// Get the return scope factory for a return value type
        /// </summary>
        /// <param name="type">Return value type</param>
        /// <returns>Return scope factory</returns>
        public static ReturnScopeFactory_Delegate? GetReturnScopeFactory(Type type)
            => ReturnScopeFactories.Keys.GetClosestType(type) is Type factoryType && 
                ReturnScopeFactories.TryGetValue(factoryType, out ReturnScopeFactory_Delegate? res) 
                ? res 
                : null;

        /// <summary>
        /// Delegate for a scope factory
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="parameter">RPC scope parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC scope</returns>
        public delegate Task<RpcProcessor.RpcScopeBase> ScopeFactory_Delegate(
            RpcProcessor processor, 
            IRpcScopeParameter parameter, 
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Delegate for a remote scope factory
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="value">RPC scope value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC remote scope</returns>
        public delegate Task<RpcProcessor.RpcRemoteScopeBase> RemoteScopeFactory_Delegate(
            RpcProcessor processor, 
            RpcScopeValue value,
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Delegate for a parameter scope factory delegate
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="parameterValue">RPC request parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC scope</returns>
        public delegate Task<RpcProcessor.RpcScopeBase?> ParameterScopeFactory_Delegate(
            RpcProcessor processor, 
            object parameterValue, 
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Delegate for a return scope factory delegate
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="returnValue">RPC method return value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC scope</returns>
        public delegate Task<RpcProcessor.RpcScopeBase?> ReturnScopeFactory_Delegate(
            RpcProcessor processor, 
            object returnValue, 
            CancellationToken cancellationToken
            );
    }
}
