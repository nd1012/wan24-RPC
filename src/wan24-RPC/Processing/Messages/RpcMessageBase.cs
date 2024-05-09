using System.Reflection;
using System.Text.Json.Serialization;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// Base type for a RPC message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcMessageBase() : StreamSerializerBase(VERSION), IRpcMessage
    {
        /// <summary>
        /// Object version number
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// <see cref="Meta"/> property
        /// </summary>
        private static readonly PropertyInfo MetaProperty;

        /// <summary>
        /// Static constructor
        /// </summary>
        static RpcMessageBase() => MetaProperty = typeof(RpcMessageBase).GetPropertyCached(nameof(Meta))!.Property;

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

        /// <inheritdoc/>
        [JsonIgnore]
        public abstract int Type { get; }

        /// <summary>
        /// Peers RPC protocol version (from the <see cref="RpcProcessorOptions.RpcVersion"/> for serializing the peers readable object structure)
        /// </summary>
        [JsonIgnore]
        public required int PeerRpcVersion { get; init; }

        /// <inheritdoc/>
        [RequiredIf(nameof(RequireId), true)]
        public long? Id { get; set; }

        /// <summary>
        /// If an <see cref="Id"/> is required
        /// </summary>
        [JsonIgnore]
        public virtual bool RequireId { get; } = true;

        /// <inheritdoc/>
        [JsonIgnore]
        public DateTime Created { get; } = DateTime.Now;

        /// <inheritdoc/>
        [CountLimit(byte.MaxValue)]
        [ItemStringLength(byte.MaxValue, ItemValidationTargets.Key)]
        [ItemStringLength(short.MaxValue >> 3)]
        public IReadOnlyDictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();

        /// <inheritdoc/>
        protected sealed override void Serialize(Stream stream) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected sealed override void Deserialize(Stream stream, int version) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteNumberAsync(HlObjectVersion, cancellationToken).DynamicContext();
            await stream.WriteNumberNullableAsync(Id, cancellationToken).DynamicContext();
            await stream.WriteDictAsync(Meta, cancellationToken).DynamicContext();
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
            Id = await stream.ReadNumberNullableAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            Meta = (await stream.ReadDictAsync<string, string>(
                version,
                maxLen: byte.MaxValue,
                keyOptions: MetaProperty.GetKeySerializerOptions(stream, version, cancellationToken),
                valueOptions: MetaProperty.GetValueSerializerOptions(stream, version, cancellationToken),
                cancellationToken: cancellationToken
                ).DynamicContext())
                .AsReadOnly();
        }
    }
}
