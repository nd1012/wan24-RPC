using System.ComponentModel.DataAnnotations;
using wan24.ObjectValidation;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope registration
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcScopeRegistration() : ValidatableRecordBase()
    {
        /// <summary>
        /// Unique RPC scope type ID (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        public required int Type { get; init; }

        /// <summary>
        /// Local scope type (must be the <see cref="RpcProcessor.RpcScopeBase"/>)
        /// </summary>
        public required Type LocalScopeType { get; init; }

        /// <summary>
        /// Remote scope type (must be the <see cref="RpcProcessor.RpcRemoteScopeBase"/>)
        /// </summary>
        public required Type RemoteScopeType { get; init; }

        /// <summary>
        /// Scope parameter factory (create a RPC scope parameter for an existing local scope instance)
        /// </summary>
        public RpcScopes.ScopeParameterFactory_Delegate? LocalScopeParameterFactory { get; init; }

        /// <summary>
        /// Local scope factory (create a scope instance from a scope parameter during a request (for a parameter value) or from a call (for the return value))
        /// </summary>
        public required RpcScopes.ScopeFactory_Delegate LocalScopeFactory { get; init; }

        /// <summary>
        /// Remote scope factory (create a scope instance from a scope value during a call (for a parameter value) or from a request (for the return value))
        /// </summary>
        public required RpcScopes.RemoteScopeFactory_Delegate RemoteScopeFactory { get; init; }

        /// <summary>
        /// Object type which needs to be scoped using a local scope (request parameters or call return values)
        /// </summary>
        [RequiredIf(nameof(ParameterLocalScopeFactory))]
        public Type? LocalScopeObjectType { get; init; }

        /// <summary>
        /// Parameter value scope factory (create a local scope instance for a request parameter value)
        /// </summary>
        public RpcScopes.ParameterScopeFactory_Delegate? ParameterLocalScopeFactory { get; init; }

        /// <summary>
        /// Object type which needs to be scoped using a remote scope (request return values or call parameters)
        /// </summary>
        [RequiredIf(nameof(ReturnLocalScopeFactory))]
        public Type? RemoteScopeObjectType { get; init; }

        /// <summary>
        /// Return value scope factory (create a local scope instance for a call return value)
        /// </summary>
        public RpcScopes.ReturnScopeFactory_Delegate? ReturnLocalScopeFactory { get; init; }

        /// <summary>
        /// Local scope RPC value type (must be a <see cref="RpcScopeValue"/>; is the allowed serialized type)
        /// </summary>
        public Type LocalScopeValueType { get; init; } = typeof(RpcScopeValue);

        /// <summary>
        /// Remote scope RPC value type (must be a <see cref="RpcScopeValue"/>; is the allowed serialized type)
        /// </summary>
        public Type RemoteScopeValueType { get; init; } = typeof(RpcScopeValue);

        /// <inheritdoc/>
        protected override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (ValidationResult result in base.Validate(validationContext))
                yield return result;
            if (!typeof(RpcProcessor.RpcScopeBase).IsAssignableFrom(LocalScopeType))
                yield return ValidationHelper.CreateValidationResult(
                    $"The {nameof(LocalScopeType)} type ({LocalScopeType}) isn't a {typeof(RpcProcessor.RpcScopeBase)}", 
                    validationContext
                    );
            if (!typeof(RpcProcessor.RpcRemoteScopeBase).IsAssignableFrom(RemoteScopeType))
                yield return ValidationHelper.CreateValidationResult(
                    $"The {nameof(RemoteScopeType)} type ({RemoteScopeType}) isn't a {typeof(RpcProcessor.RpcRemoteScopeBase)}",
                    validationContext
                    );
            if (!typeof(RpcScopeValue).IsAssignableFrom(LocalScopeValueType))
                yield return ValidationHelper.CreateValidationResult("Invalid local scope RPC value type", validationContext);
            if (!typeof(RpcScopeValue).IsAssignableFrom(RemoteScopeValueType))
                yield return ValidationHelper.CreateValidationResult("Invalid remote scope RPC value type", validationContext);
        }
    }
}
