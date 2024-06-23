using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.RPC.Processing.Options;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// RPC scope event message for the scope master
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RemoteScopeEventMessage() : SerializerScopeMessageBase(), IRpcScopeEventMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 14;

        /// <summary>
        /// Serialized arguments
        /// </summary>
        protected byte[]? _Arguments = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scope">Scope</param>
        public RemoteScopeEventMessage(in RpcProcessor.RpcRemoteScopeBase scope) : this() => ScopeId = scope.Id;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => Waiting;

        /// <inheritdoc/>
        [MinLength(1), MaxLength(byte.MaxValue)]
        public required string Name { get; set; }

        /// <inheritdoc/>
        public EventArgs? Arguments { get; set; }

        /// <inheritdoc/>
        public bool Waiting { get; set; }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
