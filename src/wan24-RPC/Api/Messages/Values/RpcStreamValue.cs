﻿using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Values
{
    /// <summary>
    /// RPC stream parameter/return value
    /// </summary>
    [Rpc]
    public record class RpcStreamValue() : StreamSerializerRecordBase(VERSION)
    {
        /// <summary>
        /// Object version
        /// </summary>
        public const int VERSION = 1;

        /// <summary>
        /// Max. stream content length in bytes
        /// </summary>
        public static int MaxContentLength { get; set; } = short.MaxValue >> 1;

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
        /// Stream ID
        /// </summary>
        [RequiredIf(nameof(Content), RequiredIfNull = true)]
        public long? Stream { get; set; }

        /// <summary>
        /// Stream content
        /// </summary>
        [RequiredIf(nameof(Stream), RequiredIfNull = true), RuntimeCountLimit("wan24.RPC.Api.Messages.Values.RpcStreamValue.MaxContentLength")]
        public byte[]? Content { get; set; }

        /// <inheritdoc/>
        protected sealed override void Serialize(Stream stream) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected sealed override void Deserialize(Stream stream, int version) => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(HlObjectVersion, cancellationToken).DynamicContext();
            await stream.WriteNumberNullableAsync(Stream, cancellationToken).DynamicContext();
            if (!Stream.HasValue)
                await stream.WriteBytesAsync(Content!, cancellationToken).DynamicContext();
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
            Stream = await stream.ReadNumberNullableAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
            if (!Stream.HasValue)
                Content = (await stream.ReadBytesAsync(version, maxLen: MaxContentLength, cancellationToken: cancellationToken).DynamicContext()).Value;
        }
    }
}
