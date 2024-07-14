using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Values;
using static wan24.Core.TranslationHelper;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC cancellation remote scope
    /// </summary>
    public class RpcCancellationRemoteScope : RpcProcessor.RpcRemoteScopeBase
    {
        /// <summary>
        /// RPC scope type ID (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        public const int TYPE = (int)RpcScopeTypes.Cancellation;

        /// <summary>
        /// Cancellation
        /// </summary>
        protected readonly CancellationTokenSource Cancellation = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="value">RPC cancellation scope value</param>
        public RpcCancellationRemoteScope(in RpcProcessor processor, in RpcCancellationScopeValue value) : base(processor, value)
        {
            HandleCreation = true;
            Token = Cancellation.Token;
            if (!value.Canceled)
                return;
            Cancellation.Cancel();
            InformMasterWhenDisposing = false;
        }

        /// <inheritdoc/>
        public override int Type => TYPE;

        /// <inheritdoc/>
        public override IEnumerable<Status> State
        {
            get
            {
                foreach (Status status in base.State)
                    yield return status;
                yield return new(__("Canceled"), Cancellation.IsCancellationRequested, __("If canceled"));
            }
        }

        /// <summary>
        /// Cancellation token
        /// </summary>
        public CancellationToken Token { get; }

        /// <inheritdoc/>
        protected override async Task<bool> HandleDiscardedAsync(IRpcScopeDiscardedMessage message, CancellationToken cancellationToken)
        {
            InformMasterWhenDisposing = false;
            return await base.HandleDiscardedAsync(message, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Cancellation.Cancel();
            Cancellation.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await base.DisposeCore().DynamicContext();
            await Cancellation.CancelAsync().DynamicContext();
            Cancellation.Dispose();
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="value">RPC scope value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scope</returns>
        public static Task<RpcProcessor.RpcRemoteScopeBase> CreateAsync(RpcProcessor processor, RpcScopeValue value, CancellationToken cancellationToken = default)
        {
            if (value is not RpcCancellationScopeValue cancellation)
                throw new InvalidProgramException($"{value.GetType()} isn't a {typeof(RpcCancellationScopeValue)}");
            return Task.FromResult<RpcProcessor.RpcRemoteScopeBase>(new RpcCancellationRemoteScope(processor, cancellation));
        }
    }
}
