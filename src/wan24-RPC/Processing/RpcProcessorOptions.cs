using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Options;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor options
    /// </summary>
    public record class RpcProcessorOptions : DisposableRecordBase
    {
        /// <summary>
        /// RPC protocol version
        /// </summary>
        public const int RPC_VERSION = 1;
        /// <summary>
        /// Min. supported RPC protocol version
        /// </summary>
        public const int MIN_RPC_VERSION = 1;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apis">APIs (will be disposed per default)</param>
        public RpcProcessorOptions(params object[] apis) : this()
        {
            if (apis.Length < 1)
                throw new ArgumentOutOfRangeException(nameof(apis));
            API = new(apis.Length);
            RpcApiInfo info;
            foreach (object api in apis)
            {
                info = new(api);
                API[info.Type.Name] = info;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apis">API types (instances will be disposed per default)</param>
        public RpcProcessorOptions(params Type[] apis) : this()
        {
            if (apis.Length < 1)
                throw new ArgumentOutOfRangeException(nameof(apis));
            API = new(apis.Length);
            RpcApiInfo info;
            foreach (Type api in apis)
            {
                info = new(api);
                API[info.Type.Name] = info;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcProcessorOptions() : base() { }

        /// <summary>
        /// An object for thread synchronization
        /// </summary>
        public object SyncObject { get; } = new();

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger? Logger { get; init; }

        /// <summary>
        /// Bi-directional RPC stream (will be disposed)
        /// </summary>
        public required Stream Stream { get; init; }

        /// <summary>
        /// Flush the <see cref="Stream"/> after sending a message?
        /// </summary>
        public bool FlushStream { get; init; }

        /// <summary>
        /// Stream serializer version
        /// </summary>
        public int SerializerVersion { get; init; } = StreamSerializer.Version;

        /// <summary>
        /// Max. message length (I/O) in bytes at both peers
        /// </summary>
        public int MaxMessageLength { get; init; } = Settings.BufferSize;

        /// <summary>
        /// Peer RPC protocol version
        /// </summary>
        public int RpcVersion { get; set; } = RPC_VERSION;

        /// <summary>
        /// API (infos will be disposed)
        /// </summary>
        public Dictionary<string, RpcApiInfo> API { get; init; } = null!;

        /// <summary>
        /// Default context for an incoming RPC call (will be disposed)
        /// </summary>
        public RpcContext? DefaultContext { get; init; }

        /// <summary>
        /// Default service provider for an incoming RPC call (will be disposed)
        /// </summary>
        public IServiceProvider? DefaultServices { get; init; }

        /// <summary>
        /// Disconnect the peer on API error (when processing RPC requests)?
        /// </summary>
        public bool DisconnectOnApiError { get; init; }

        /// <summary>
        /// Keep alive options
        /// </summary>
        public KeepAliveOptions? KeepAlive { get; init; }

        /// <summary>
        /// RPC message priority options
        /// </summary>
        public MessagePriorityOptions Priorities { get; init; } = new();

        /// <summary>
        /// Incoming message queue options
        /// </summary>
        public required ParallelQueueOptions IncomingMessageQueue { get; init; }

        /// <summary>
        /// Outgoing message queue capacity (for priority message sending)
        /// </summary>
        public required int OutgoingMessageQueueCapacity { get; init; }

        /// <summary>
        /// Call queue options
        /// </summary>
        public required ParallelQueueOptions CallQueue { get; init; }

        /// <summary>
        /// Request queue options
        /// </summary>
        public required ParallelQueueOptions RequestQueue { get; init; }

        /// <summary>
        /// Use scopes?
        /// </summary>
        public bool UseScopes { get; init; } = true;

        /// <summary>
        /// Stream options
        /// </summary>
        public StreamScopeOptions? Streams { get; init; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Stream.Dispose();
            DefaultContext?.Dispose();
            DefaultServices?.TryDispose();
            API.Values.DisposeAll();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await Stream.DisposeAsync().DynamicContext();
            if (DefaultContext is not null)
                await DefaultContext.DisposeAsync().DynamicContext();
            if (DefaultServices is not null)
                await DefaultServices.TryDisposeAsync().DynamicContext();
            await API.Values.DisposeAllAsync().DynamicContext();
        }
    }
}
