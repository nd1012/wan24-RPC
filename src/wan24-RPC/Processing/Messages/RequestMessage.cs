using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Options;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC request message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RequestMessage() : SerializerRpcMessageBase(), IRpcRequest
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 0;

        /// <summary>
        /// An object for thread synchronization
        /// </summary>
        protected readonly object SyncObject = new();
        /// <summary>
        /// Serialized parameters
        /// </summary>
        protected byte[]? _Parameters = null;
        /// <summary>
        /// If the parameters are disposed
        /// </summary>
        protected bool ParametersDisposed = false;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// If a return value is expected
        /// </summary>
        public virtual bool WantsReturnValue { get; set; } = true;

        /// <summary>
        /// API name
        /// </summary>
        [MinLength(1), MaxLength(byte.MaxValue)]
        public string? Api { get; set; }

        /// <summary>
        /// API method name
        /// </summary>
        [MinLength(1), MaxLength(byte.MaxValue)]
        public required string Method { get; set; }

        /// <summary>
        /// API method parameters
        /// </summary>
        [CountLimit(byte.MaxValue)]
        public object?[]? Parameters { get; set; }

        /// <summary>
        /// Deserialize parameters for a RPC method call
        /// </summary>
        /// <param name="method">RPC API method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task DeserializeParametersAsync(RpcApiMethodInfo method, CancellationToken cancellationToken = default)
        {
            if (_Parameters is null)
                throw new InvalidOperationException();
            using MemoryStream ms = new(_Parameters);
            Parameters = await DeserializeObjectListAsync(ms, [.. method.RpcParameters.Select(p => p.Parameter.ParameterType)], cancellationToken).DynamicContext();
            _Parameters = null;
        }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteAsync(WantsReturnValue, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Api, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Method, cancellationToken).DynamicContext();
            using MemoryPoolStream ms = new();
            await SerializeObjectListAsync(ms, Parameters, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(ms.Length, cancellationToken).DynamicContext();
            ms.Position = 0;
            await ms.CopyToAsync(stream, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            WantsReturnValue = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            Api = await stream.ReadStringNullableAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Method = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            ushort len = await stream.ReadNumberAsync<ushort>(version, cancellationToken: cancellationToken).DynamicContext();
            if (len < 1 || len > StreamScopeOptions.MaxStreamContentLength)
                throw new InvalidDataException($"Invalid serialized parameters length {len}");
            _Parameters = new byte[len];
            await stream.ReadExactlyAsync(_Parameters, cancellationToken).DynamicContext();
        }
    }
}
