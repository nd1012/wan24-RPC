using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages
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
        /// Event arguments
        /// </summary>
        public EventArgs? Arguments { get; set; }

        /// <summary>
        /// If the sender is waiting for the event hndlers to finish
        /// </summary>
        public bool Waiting { get; set; }

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
            await SerializeObjectAsync(stream, Arguments, cancellationToken).DynamicContext();
            await stream.WriteAsync(Waiting, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Name = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            object? arguments = await DeserializeObjectAsync(stream, cancellationToken).DynamicContext();
            if (arguments is not null)
            {
                Arguments = arguments as EventArgs;
                if(Arguments is null)
                {
                    await arguments.TryDisposeAsync().DynamicContext();
                    throw new InvalidDataException($"Invalid event arguments {arguments.GetType()}");
                }
            }
            Waiting = await stream.ReadBoolAsync(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
