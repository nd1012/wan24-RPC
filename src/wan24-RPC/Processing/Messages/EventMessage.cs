using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.RPC.Processing.Options;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC event message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class EventMessage() : SerializerRpcMessageBase(), IRpcRequest
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 4;

        /// <summary>
        /// Serialized arguments
        /// </summary>
        protected byte[]? _Arguments = null;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => Waiting;

        /// <summary>
        /// Event name
        /// </summary>
        [MinLength(1), MaxLength(byte.MaxValue)]
        public required string Name { get; set; }

        /// <summary>
        /// Event arguments (will be disposed at the receiver side)
        /// </summary>
        public EventArgs? Arguments { get; set; }

        /// <summary>
        /// If there are serialized event arguments that need to be deserialized
        /// </summary>
        public bool HasSerializedArguments => _Arguments is not null;

        /// <summary>
        /// If the sender is waiting for the event handlers to finish
        /// </summary>
        public bool Waiting { get; set; }

        /// <summary>
        /// Deserialize the arguments
        /// </summary>
        /// <param name="type">Arguments type (must be am <see cref="EventArgs"/>)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task DeserializeArgumentsAsync(Type type, CancellationToken cancellationToken = default)
        {
            if (_Arguments is null)
                throw new InvalidOperationException();
            using MemoryStream ms = new(_Arguments);
            object? arguments = await DeserializeObjectAsync(ms, type, cancellationToken).DynamicContext();
            if (arguments is not null)
            {
                Arguments = arguments as EventArgs;
                if (Arguments is null)
                {
                    await arguments.TryDisposeAsync().DynamicContext();
                    throw new InvalidDataException($"Invalid event arguments {arguments.GetType()}");
                }
            }
            _Arguments = null;
        }

        /// <summary>
        /// Dispose the arguments
        /// </summary>
        public virtual async Task DisposeArgumentsAsync()
        {
            if (Arguments is not null)
                await Arguments.TryDisposeAsync().DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Name, cancellationToken).DynamicContext();
            await stream.WriteAsync(Waiting, cancellationToken).DynamicContext();
            using MemoryPoolStream ms = new();
            await SerializeObjectAsync(ms, Arguments, cancellationToken).DynamicContext();
            ms.Position = 0;
            await stream.WriteNumberAsync(ms.Length, cancellationToken).DynamicContext();
            await ms.CopyToAsync(stream, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Name = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Waiting = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
            ushort len = await stream.ReadNumberAsync<ushort>(version, cancellationToken: cancellationToken).DynamicContext();
            if (len < 1 || len > StreamScopeOptions.MaxStreamContentLength)
                throw new InvalidDataException($"Invalid serialized arguments length {len}");
            _Arguments = new byte[len];
            await stream.ReadExactlyAsync(_Arguments, cancellationToken).DynamicContext();
        }
    }
}
