using Microsoft.Extensions.Logging;
using wan24.Core;

namespace wan24.RPC.Processing
{
    // Helper methods
    public partial class RpcProcessor
    {
        /// <summary>
        /// Stop exceptional and dispose
        /// </summary>
        /// <param name="ex">Exception</param>
        protected virtual async Task StopExceptionalAndDisposeAsync(Exception ex)
        {
            Logger?.Log(LogLevel.Error, "{this} stop exceptional: {ex}", ToString(), ex);
            if (StoppedExceptional || LastException is not null)
            {
                Logger?.Log(LogLevel.Warning, "{this} had an exception already", ToString());
                return;
            }
            StoppedExceptional = true;
            LastException = ex;
            await DisposeAsync().DynamicContext();
        }

        /// <summary>
        /// Ensure streams are enabled
        /// </summary>
        protected virtual void EnsureStreamsAreEnabled()
        {
            if (Options.MaxStreamCount < 1)
                throw new InvalidOperationException("Streams are disabled");
        }
    }
}
