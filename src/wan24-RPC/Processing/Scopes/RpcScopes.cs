using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Reflection;
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
        /// Registered scopes (key is the scope type ID (see <see cref="RpcScopeTypes"/>))
        /// </summary>
        public static readonly Dictionary<int, RpcScopeRegistration> Registered = [];

        /// <summary>
        /// Constructor
        /// </summary>
        static RpcScopes()
        {
            // Simple scope
            RegisterScope(new()
            {
                Type = (int)RpcScopeTypes.Scope,
                LocalScopeType = typeof(RpcScope),
                RemoteScopeType = typeof(RpcRemoteScope),
                LocalScopeFactory = RpcScope.CreateAsync,
                RemoteScopeFactory = RpcRemoteScope.CreateAsync,
                LocalScopeParameterFactory = RpcScopeParameter.CreateAsync
            });
            //TODO Register more built-in scope factories
        }

        /// <summary>
        /// Register a RPC scope
        /// </summary>
        /// <param name="registration">Registration</param>
        public static void RegisterScope(RpcScopeRegistration registration)
        {
            try
            {
                registration.ValidateObject(out _);
            }
            catch(ObjectValidationException ex)
            {
                throw new ArgumentException("Invalid RPC scope registration", nameof(registration), ex);
            }
            if (Registered.ContainsKey(registration.Type))
                throw new InvalidOperationException($"A RPC scope type ID {registration.Type} was registered already");
            if (Registered.Values.FirstOrDefault(r => r.LocalScopeType == registration.LocalScopeType) is RpcScopeRegistration localScope)
                throw new InvalidOperationException($"RPC scope type #{localScope.Type} uses the local scope type {registration.LocalScopeType} already");
            if (Registered.Values.FirstOrDefault(r => r.RemoteScopeType == registration.RemoteScopeType) is RpcScopeRegistration remoteScope)
                throw new InvalidOperationException($"RPC scope type #{remoteScope.Type} uses the remote scope type {registration.RemoteScopeType} already");
            if (
                registration.LocalScopeObjectType is not null && 
                Registered.Values.FirstOrDefault(r => r.LocalScopeObjectType == registration.LocalScopeObjectType) is RpcScopeRegistration localValue
                )
                throw new InvalidOperationException($"RPC scope type #{localValue.Type} uses the parameter type {registration.LocalScopeObjectType} already");
            if (
                registration.RemoteScopeObjectType is not null && 
                Registered.Values.FirstOrDefault(r => r.RemoteScopeObjectType == registration.RemoteScopeObjectType) is RpcScopeRegistration remoteValue
                )
                throw new InvalidOperationException($"RPC scope type #{remoteValue.Type} uses the return type {registration.RemoteScopeObjectType} already");
            Registered[registration.Type] = registration;
        }

        /// <summary>
        /// Get a local scope factory from a scope type ID
        /// </summary>
        /// <param name="type">Scope type ID (see <see cref="RpcScopeTypes"/>)</param>
        /// <returns>Factory</returns>
        public static ScopeFactory_Delegate? GetLocalScopeFactory(in int type)
            => Registered.TryGetValue(type, out RpcScopeRegistration? registration)
                ? registration.LocalScopeFactory
                : null;

        /// <summary>
        /// Get a remote scope factory from a scope type ID
        /// </summary>
        /// <param name="type">Scope type ID (see <see cref="RpcScopeTypes"/>)</param>
        /// <returns>Factory</returns>
        public static RemoteScopeFactory_Delegate? GetRemoteScopeFactory(in int type)
            => Registered.TryGetValue(type, out RpcScopeRegistration? registration)
                ? registration.RemoteScopeFactory
                : null;

        /// <summary>
        /// Get the RPC scope parameter factory for a scope
        /// </summary>
        /// <param name="type">Local RPC scope type (see <see cref="RpcProcessor.RpcScopeBase"/>)</param>
        /// <returns>Factory</returns>
        public static ScopeParameterFactory_Delegate? GetScopeParameterFactory(in int type)
            => Registered.TryGetValue(type, out RpcScopeRegistration? registration)
                ? registration.LocalScopeParameterFactory
                : null;

        /// <summary>
        /// Get the parameter scope factory for a parameter value type
        /// </summary>
        /// <param name="type">Parameter value type</param>
        /// <returns>Parameter scope factory</returns>
        public static ParameterScopeFactory_Delegate? GetParameterScopeFactory(Type type)
            => Registered.Values.Where(r => r.ParameterLocalScopeFactory is not null)
                .Select(r => r.LocalScopeObjectType)
                .WhereNotNull()
                .GetClosestType(type) is Type objectType
                ? Registered.Values.First(r => r.LocalScopeObjectType == objectType).ParameterLocalScopeFactory
                : null;

        /// <summary>
        /// Get the return scope factory for a return value type
        /// </summary>
        /// <param name="type">Return value type</param>
        /// <returns>Return scope factory</returns>
        public static ReturnScopeFactory_Delegate? GetReturnScopeFactory(Type type)
            => Registered.Values.Where(r => r.ReturnLocalScopeFactory is not null)
                .Select(r => r.LocalScopeObjectType)
                .WhereNotNull()
                .GetClosestType(type) is Type objectType
                ? Registered.Values.First(r => r.LocalScopeObjectType == objectType).ReturnLocalScopeFactory
                : null;

        /// <summary>
        /// Determine if a type is scoped
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>If the type is scoped</returns>
        public static bool IsScopedType(in Type type)
            => Registered.Values.Select(r => r.LocalScopeObjectType).WhereNotNull().GetClosestType(type) is not null ||
                Registered.Values.Select(r => r.RemoteScopeObjectType).WhereNotNull().GetClosestType(type) is not null;

        /// <summary>
        /// Determine if a type is a scope element type (a parameter, value or scope)
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>if the type is a scope element</returns>
        public static bool IsScopeElement(in Type type)
            => typeof(RpcScopeParameterBase).IsAssignableFrom(type) ||
                typeof(DisposableRpcScopeParameterBase).IsAssignableFrom(type) ||
                typeof(RpcScopeValue).IsAssignableFrom(type) || 
                typeof(RpcProcessor.RpcScopeProcessorBase).IsAssignableFrom(type);

        /// <summary>
        /// Get the allowed serialized value type for a parameter or return type
        /// </summary>
        /// <param name="type">Value type</param>
        /// <param name="isRemote">If a value of the <c>type</c> is being received from the remote</param>
        /// <returns>Allowed serialized value type (which may be the given <c>type</c> or a <see cref="RpcScopeValue"/> type)</returns>
        public static Type GetAllowedSerializedType(Type type, in bool isRemote = true)
        {
            // A scope value is the only possible serialized value type
            if (typeof(RpcScopeValue).IsAssignableFrom(type)) return type;
            // Scope type handling
            if (typeof(RpcProcessor.RpcScopeProcessorBase).IsAssignableFrom(type))
                if (typeof(RpcProcessor.RpcScopeBase).IsAssignableFrom(type))
                {
                    if (isRemote)
                        throw new InvalidProgramException($"{type} can't be received from the remote");
                    if (Registered.Values.FirstOrDefault(r => r.LocalScopeType.IsAssignableFrom(type)) is RpcScopeRegistration scopeRegistration)
                        return scopeRegistration.LocalScopeValueType;
                }
                else if (typeof(RpcProcessor.RpcRemoteScopeBase).IsAssignableFrom(type))
                {
                    if (!isRemote)
                        throw new InvalidProgramException($"{type} can't be sent to the remote");
                    if (Registered.Values.FirstOrDefault(r => r.RemoteScopeType.IsAssignableFrom(type)) is RpcScopeRegistration scopeRegistration)
                        return scopeRegistration.RemoteScopeValueType;
                }
            // If there's a local scope which serves the given type, the serialized value type must be its scope value
            if (!isRemote && Registered.Values.FirstOrDefault(r => r.LocalScopeObjectType?.IsAssignableFrom(type) ?? false) is RpcScopeRegistration scopeRegistration2)
                return scopeRegistration2.LocalScopeValueType;
            // If there's a remote scope which serves the given type, the serialized value type must be its scope value
            if (
                isRemote && 
                Registered.Values
                    .FirstOrDefault(r => r.RemoteScopeObjectType is not null && type.IsAssignableFrom(r.RemoteScopeObjectType)) is RpcScopeRegistration scopeRegistration3
                )
                return scopeRegistration3.RemoteScopeValueType;
            return type;
        }

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
        /// <param name="method">RPC API method which returned the value</param>
        /// <param name="returnValue">RPC API method return value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC scope</returns>
        public delegate Task<RpcProcessor.RpcScopeBase?> ReturnScopeFactory_Delegate(
            RpcProcessor processor, 
            RpcApiMethodInfo method,
            object returnValue, 
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Delegate for a scope parameter factory
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="scope">RPC scope</param>
        /// <param name="apiMethod">RPC API method (for a return value; if <see langword="null"/>, the scope was used as request parameter)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scope parameter (will be set to <see cref="RpcProcessor.RpcScopeBase.ScopeParameter"/>, also)</returns>
        public delegate Task<IRpcScopeParameter> ScopeParameterFactory_Delegate(
            RpcProcessor processor,
            RpcProcessor.RpcScopeBase scope,
            RpcApiMethodInfo? apiMethod,
            CancellationToken cancellationToken
            );
    }
}
