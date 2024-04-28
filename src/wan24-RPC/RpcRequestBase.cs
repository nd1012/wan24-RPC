using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC
{
    /// <summary>
    /// Base type for a RPC request message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcRequestBase() : RpcMessageBase()
    {
        /// <inheritdoc/>
        public override bool RequireId => WantsResponse;

        /// <summary>
        /// If a response is being awaited (if <see langword="true"/>, a return value and/or an exception response will be awaited and processed)
        /// </summary>
        public virtual bool WantsResponse { get; set; }

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

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteAsync(WantsResponse, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Api, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Method, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            WantsResponse = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            if (WantsResponse && !Id.HasValue)
                throw new InvalidDataException($"{GetType()} want's reponse but doesn't have an ID");
            Api = await stream.ReadStringNullableAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Method = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
