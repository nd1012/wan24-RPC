using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Values
{
    /// <summary>
    /// RPC enumerable parameter
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    [Rpc]
    public record class RpcEnumerableValue<T>() : StreamSerializerRecordBase(VERSION)
    {
        /// <summary>
        /// Object version
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// Max. item count
        /// </summary>
        public static int MaxItemCount { get; set; } = 3;

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
        public virtual int HlObjectVersion { get; } = 1;

        /// <summary>
        /// Enumeration ID
        /// </summary>
        [RequiredIf(nameof(Enumerable), RequiredIfNull = true)]
        public long? Id { get; set; }

        /// <summary>
        /// Enumerable
        /// </summary>
        [RequiredIf(nameof(Id), RequiredIfNull = true)]
        public IEnumerable<T>? Enumerable { get; set; }

        /// <inheritdoc/>
        protected sealed override void Serialize(Stream stream) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected sealed override void Deserialize(Stream stream, int version) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(HlObjectVersion, cancellationToken).DynamicContext();
            await stream.WriteNumberNullableAsync(Id, cancellationToken).DynamicContext();
            if (!Id.HasValue)
                await stream.WriteArrayAsync([.. Enumerable], cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            if (!SerializedObjectVersion.HasValue)
                throw new InvalidDataException($"{GetType()} is missing the serialized object version");
            if (SerializedObjectVersion.Value < 1 || SerializedObjectVersion.Value > VERSION)
                throw new InvalidDataException($"Unsupported {GetType()} object version #{SerializedObjectVersion}");
            SerializedHlObjectVersion = await stream.ReadNumberAsync<int>(version, cancellationToken: cancellationToken).DynamicContext();
            if (SerializedHlObjectVersion < (MinHlObjectVersion ?? 1) || SerializedHlObjectVersion > HlObjectVersion)
                throw new InvalidDataException($"Unsupported {GetType()} higher level object version #{SerializedHlObjectVersion}");
            Id = await stream.ReadNumberNullableAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            if (!Id.HasValue)
                Enumerable = await stream.ReadArrayAsync<T>(version, maxLen: MaxItemCount, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
