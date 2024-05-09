using wan24.RPC.Processing;

namespace wan24.RPC.Api
{
    /// <summary>
    /// Interface for a RPC API type which wants to get hosting RPC processor informations after construction
    /// </summary>
    public interface IWantRpcProcessorInfo
    {
        /// <summary>
        /// RPC processor
        /// </summary>
        public RpcProcessor? Processor { get; set; }

        /// <summary>
        /// RPC processor cancellation token
        /// </summary>
        public CancellationToken ProcessorCancellation { get; set; }
    }
}
