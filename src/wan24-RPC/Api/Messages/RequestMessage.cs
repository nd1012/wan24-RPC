using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.RPC.Api.Reflection;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages
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
        /// If the parameters are disposed
        /// </summary>
        protected bool ParametersDisposed = false;
        /// <summary>
        /// If a response is being awaited (if <see langword="true"/>, a return value and/or an exception response will be awaited and processed)
        /// </summary>
        protected bool _WantsResponse = false;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// If a response is being awaited (if <see langword="true"/>, a return value and/or an exception response will be awaited and processed)
        /// </summary>
        public virtual bool WantsResponse
        {
            get => _WantsResponse || WantsReturnValue;
            set => _WantsResponse = value;
        }

        /// <summary>
        /// If a return value is expected (<see cref="WantsResponse"/> must be <see langword="true"/> for this)
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
        /// Dispose parameter values
        /// </summary>
        /// <param name="method">Method</param>
        public virtual async Task DisposeParametersAsync(RpcApiMethodInfo? method = null)
        {
            if (Parameters is null || ParametersDisposed)
                return;
            ParametersDisposed = true;
            if (method is null)
            {
                foreach (object? obj in Parameters)
                    if (obj is not null)
                        await obj.TryDisposeAsync().DynamicContext();
                return;
            }
            for (int i = 0, len = method.RpcParameters.Count, max = Parameters.Length; i < len && i < max; i++)
            {
                if (Parameters[i] is null) continue;
                if (
                    !method.RpcParameters.Items[i].Parameter.ParameterType.IsAssignableFrom(Parameters[i]!.GetType()) ||
                    method.RpcParameters.Items[i].DisposeParameterValue
                    )
                    await Parameters[i]!.TryDisposeAsync().DynamicContext();
            }
            if (method.RpcParameters.Count < Parameters.Length)
                foreach (object? obj in Parameters.Skip(method.RpcParameters.Count))
                    if (obj is not null)
                        await obj.TryDisposeAsync().DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteAsync(WantsResponse, cancellationToken).DynamicContext();
            await stream.WriteAsync(WantsReturnValue, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Api, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Method, cancellationToken).DynamicContext();
            await SerializeObjectListAsync(stream, Parameters, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            WantsResponse = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            if (WantsResponse && !Id.HasValue)
                throw new InvalidDataException($"{GetType()} want's reponse but doesn't have an ID");
            WantsReturnValue = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            Api = await stream.ReadStringNullableAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Method = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            try
            {
                Parameters = await DeserializeObjectListAsync(stream, cancellationToken).DynamicContext();
            }
            catch
            {
                await DisposeParametersAsync().DynamicContext();
                throw;
            }
        }
    }
}
