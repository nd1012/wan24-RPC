using System.Collections.Concurrent;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    public sealed class TestRpcProcessor(RpcProcessorOptions options) : RpcProcessor(options)
    {
        public wan24.Core.Timeout? ServerHeartBeat => HeartBeat;

        public wan24.Core.Timeout? ClientHeartBeat => PeerHeartBeat;

        public long CurrentScopeId => ScopeId;

        public int CallCount => Calls.Count;

        public ConcurrentDictionary<long, RpcScopeBase> LocalScopes => Scopes;

        public ConcurrentDictionary<long, RpcRemoteScopeBase> PeerScopes => RemoteScopes;
    }
}
