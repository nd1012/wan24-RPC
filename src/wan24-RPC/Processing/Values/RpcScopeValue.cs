using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Values
{
    /// <summary>
    /// RPC scope value
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [Rpc]
    public record class RpcScopeValue() : StreamSerializerRecordBase(VERSION)
    {
        /// <summary>
        /// Object version
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// Minimum supported higher level object version (see <see cref="SerializedHlObjectVersion"/>)
        /// </summary>
        protected virtual int? MinHlObjectVersion { get; }

        /// <summary>
        /// Deserialized higher level object version
        /// </summary>
        protected int? SerializedHlObjectVersion { get; private set; }

        /// <summary>
        /// Higher level object version
        /// </summary>
        [JsonIgnore]
        public virtual int HlObjectVersion { get; } = 1;

        /// <summary>
        /// Parameter
        /// </summary>
        [JsonIgnore]
        public IRpcScopeParameter? Parameter { get; init; }

        /// <summary>
        /// Scope ID
        /// </summary>
        [Range(1, long.MaxValue)]
        public required long Id { get; set; }

        /// <summary>
        /// Scope key
        /// </summary>
        [StringLength(byte.MaxValue, MinimumLength = 1)]
        public string? Key { get; set; }

        /// <summary>
        /// Replace an existing keyed scope (will be disposed)?
        /// </summary>
        public bool ReplaceExistingScope { get; set; }

        /// <summary>
        /// If the scope should be stored
        /// </summary>
        public bool IsStored { get; set; }

        /// <summary>
        /// Scope type (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        [Range(0, int.MaxValue)]
        public required int Type { get; set; }

        /// <summary>
        /// Dispose the scope value when done?
        /// </summary>
        public bool DisposeScopeValue { get; set; } = true;

        /// <summary>
        /// If to dispose the scope value on error
        /// </summary>
        public bool DisposeScopeValueOnError { get; set; } = true;

        /// <summary>
        /// Inform the scope master when disposing the scope value
        /// </summary>
        public bool InformMasterWhenDisposing { get; set; }

        /// <inheritdoc/>
        protected sealed override void Serialize(Stream stream) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected sealed override void Deserialize(Stream stream, int version) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteNumberAsync(HlObjectVersion, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Id, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Key, cancellationToken).DynamicContext();
            await stream.WriteAsync(ReplaceExistingScope, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Type, cancellationToken).DynamicContext();
            await stream.WriteAsync(IsStored, cancellationToken).DynamicContext();
            await stream.WriteAsync(DisposeScopeValue, cancellationToken).DynamicContext();
            await stream.WriteAsync(DisposeScopeValueOnError, cancellationToken).DynamicContext();
            await stream.WriteAsync(InformMasterWhenDisposing, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            if (!SerializedObjectVersion.HasValue)
                throw new InvalidDataException($"{GetType()} is missing the serialized object version");
            if (SerializedObjectVersion.Value < 1 || SerializedObjectVersion.Value > VERSION)
                throw new InvalidDataException($"Unsupported {GetType()} object version #{SerializedObjectVersion}");
            SerializedHlObjectVersion = await stream.ReadNumberAsync<int>(version, cancellationToken: cancellationToken).DynamicContext();
            if (SerializedHlObjectVersion < (MinHlObjectVersion ?? 1) || SerializedHlObjectVersion > HlObjectVersion)
                throw new InvalidDataException($"Unsupported {GetType()} higher level object version #{SerializedHlObjectVersion}");
            Id = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            Key = await stream.ReadStringNullableAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            ReplaceExistingScope = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            Type = await stream.ReadNumberAsync<int>(version, cancellationToken: cancellationToken).DynamicContext();
            IsStored = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            DisposeScopeValue = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            DisposeScopeValueOnError = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            InformMasterWhenDisposing = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
