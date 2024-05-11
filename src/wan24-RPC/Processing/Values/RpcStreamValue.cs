using System.Text.Json.Serialization;
using wan24.Compression;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Values
{
    /// <summary>
    /// RPC stream parameter/return value
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [Rpc]
    public record class RpcStreamValue() : DisposableStreamSerializerRecordBase(VERSION)
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
        /// Compression options
        /// </summary>
        protected byte[]? _Compression = null;

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
        /// Stream ID
        /// </summary>
        [RequiredIf(nameof(Content), RequiredIfNull = true)]
        public long? Stream { get; set; }

        /// <summary>
        /// Stream content
        /// </summary>
        [RequiredIf(nameof(Stream), RequiredIfNull = true), RuntimeCountLimit("wan24.RPC.Api.Messages.Values.RpcStreamValue.MaxContentLength")]
        public byte[]? Content { get; set; }

        /// <summary>
        /// Uncompressed stream length in bytes
        /// </summary>
        public long? Length { get; set; }

        /// <summary>
        /// Compression options
        /// </summary>
        public CompressionOptions? Compression
        {
            get
            {
                if (_Compression is null)
                    return null;
                using MemoryStream ms = new(_Compression);
                return CompressionHelper.ReadOptions(ms, System.IO.Stream.Null);
            }
            set
            {
                if (value is null)
                {
                    _Compression = null;
                    return;
                }
                using MemoryPoolStream ms = new();
                CompressionHelper.WriteOptions(System.IO.Stream.Null, ms, value);
                _Compression = ms.ToArray();
            }
        }

        /// <summary>
        /// If compression is being used
        /// </summary>
        [JsonIgnore]
        public bool UsesCompression => _Compression is not null;

        /// <summary>
        /// Incoming stream to dispose when this value disposes
        /// </summary>
        [JsonIgnore]
        public RpcProcessor.IncomingStream? IncomingStream { get; set; }

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
            if (Content is null)
            {
                await stream.WriteNumberNullableAsync(Length, cancellationToken).DynamicContext();
                await stream.WriteBytesNullableAsync(_Compression, cancellationToken).DynamicContext();
            }
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
            if (Content is null)
            {
                Length = await stream.ReadNumberNullableAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
                _Compression = (await stream.ReadBytesAsync(version, cancellationToken: cancellationToken).DynamicContext()).Value;
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            IncomingStream?.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (IncomingStream is not null)
                await IncomingStream.DisposeAsync().DynamicContext();
        }
    }
}
