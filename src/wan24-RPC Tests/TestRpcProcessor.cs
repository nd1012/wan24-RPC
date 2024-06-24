using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    public sealed class TestRpcProcessor(RpcProcessorOptions options) : RpcProcessor(options)
    {
        public wan24.Core.Timeout? ServerHeartBeat => HeartBeat;

        public wan24.Core.Timeout? ClientHeartBeat => PeerHeartBeat;
    }
}
