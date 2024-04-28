﻿using wan24.Core;
using wan24.ObjectValidation;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC
{
    /// <summary>
    /// Base type for a RPC message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcMessageBase() : StreamSerializerBase(VERSION)
    {
        /// <summary>
        /// Object version number
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// Minimum supported higher level object version (see <see cref="SerializedHlObjectVersion"/>)
        /// </summary>
        protected virtual int? MinHlObjectVersion { get; }

        /// <summary>
        /// Deserialized higher level object version
        /// </summary>
        protected int? SerializedHlObjectVersion{ get; private set; }

        /// <summary>
        /// Higher level object version
        /// </summary>
        public virtual int HlObjectVersion { get; } = 1;

        /// <summary>
        /// Type ID
        /// </summary>
        public abstract int Type { get; }

        /// <summary>
        /// ID
        /// </summary>
        [RequiredIf(nameof(RequireId), true)]
        public long? Id { get; set; }

        /// <summary>
        /// If an <see cref="Id"/> is required
        /// </summary>
        public virtual bool RequireId { get; } = true;

        /// <inheritdoc/>
        protected sealed override void Serialize(Stream stream) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected sealed override void Deserialize(Stream stream, int version) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(HlObjectVersion, cancellationToken).DynamicContext();
            if (Id.HasValue)
                await stream.WriteNumberAsync(Id.Value, cancellationToken).DynamicContext();
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
            if (RequireId)
                Id = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
