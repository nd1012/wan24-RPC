using wan24.RPC.Api.Messages;

namespace wan24.RPC.Processing
{
    // Remote event
    public partial class RpcProcessor
    {
        /// <summary>
        /// Handle a RPC event (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleEventAsync(EventMessage message)
        {

        }
    }
}
