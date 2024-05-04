﻿using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor options
    /// </summary>
    public record class RpcProcessorOptions : DisposableRecordBase
    {
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
            foreach(object api in apis)
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
        public bool FlushStream { get; init; } = true;

        /// <summary>
        /// Stream serializer version
        /// </summary>
        public int SerializerVersion { get; init; } = StreamSerializer.Version;

        /// <summary>
        /// Peer API version
        /// </summary>
        public int ApiVersion { get; set; } = 1;

        /// <summary>
        /// API (infos will be disposed)
        /// </summary>
        public Dictionary<string, RpcApiInfo> API { get; init; } = null!;

        /// <summary>
        /// Default context for an incoming RPC call (will be disposed)
        /// </summary>
        public RpcContext? DefaultContext { get; init; }

        /// <summary>
        /// Max. number of queued RPC requests (RPC requests from the peer; should at last fit the peers <see cref="RequestThreads"/>)
        /// </summary>
        public required int CallQueueSize { get; init; }

        /// <summary>
        /// Max. number of RPC request processing threads
        /// </summary>
        public required int CallThreads { get; init; }

        /// <summary>
        /// Default service provider for an incoming RPC call (will be disposed)
        /// </summary>
        public IServiceProvider? DefaultServices { get; init; }

        /// <summary>
        /// Disconnect the peer on API error (when processing RPC requests)?
        /// </summary>
        public bool DisconnectOnApiError { get; init; }

        /// <summary>
        /// Max. number of queued RPC calls (RPC requests to the peer)
        /// </summary>
        public required int RequestQueueSize { get; init; }

        /// <summary>
        /// Max. number of RPC call processing threads (should not exceed the peers <see cref="CallQueueSize"/>)
        /// </summary>
        public required int RequestThreads { get; init; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Stream.Dispose();
            DefaultContext?.Dispose();
            DefaultServices?.TryDispose();
            API.Values.DisposeAll();
            API.Clear();
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
            API.Clear();
        }
    }
}
