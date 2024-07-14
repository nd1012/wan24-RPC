using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;
using static wan24.Core.TranslationHelper;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC cancellation scope (will be discarded + disposed on cancellation)
    /// </summary>
    public class RpcCancellationScope : RpcProcessor.RpcScopeBase
    {
        /// <summary>
        /// RPC scope type ID (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        public const int TYPE = (int)RpcScopeTypes.Cancellation;

        /// <summary>
        /// Cancellation
        /// </summary>
        protected readonly CancellationTokenSource? Cancellation;
        /// <summary>
        /// Cancellation registration
        /// </summary>
        protected readonly CancellationTokenRegistration? Registration;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="key">RPC scope key</param>
        /// <param name="token">Cancellation token</param>
        public RpcCancellationScope(in RpcProcessor processor, in string? key = null, in CancellationToken token = default) : base(processor, key)
        {
            _DisposeValue = false;
            Cancellation = token.IsEqualTo(default) ? new() : null;
            Token = Cancellation?.Token ?? token;
            Registration = Token.IsCancellationRequested 
                ? null 
                : Token.Register(HandleCancellationAsync);
            IsStored = !Token.IsCancellationRequested;
            InformConsumerWhenDisposing = IsStored;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="key">RPC scope key</param>
        public RpcCancellationScope(in RpcProcessor processor, in RpcCancellationScopeParameter parameter, in string? key = null) : this(processor, key, parameter.Token)
            => SetScopeParameter(parameter);

        /// <inheritdoc/>
        public override int Type => TYPE;

        /// <inheritdoc/>
        public override object? Value => Token;

        /// <inheritdoc/>
        public override IEnumerable<Status> State
        {
            get
            {
                foreach (Status status in base.State)
                    yield return status;
                yield return new(__("Cancellation"), Cancellation is not null, __("If a cancellation token source was created"));
                yield return new(__("Registration"), Registration is not null, __("If a cancellation token registration was created"));
                yield return new(__("Requested"), Token.IsCancellationRequested, __("If the cancellation token requests cancellation"));
                yield return new(__("Canceled"), WasCanceled, __("If the cancellation token was cancelled during being scoped"));
                yield return new(__("Triggered"), WasTriggered, __("If the peer stored the remote scope"));
            }
        }

        /// <summary>
        /// Cancellation token
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// If the <see cref="Token"/> was canceled during being scoped
        /// </summary>
        public bool WasCanceled { get; protected set; }

        /// <summary>
        /// If the scope was triggered (the peer stored the remote scope)
        /// </summary>
        public bool WasTriggered { get; protected set; }

        /// <summary>
        /// Cancel
        /// </summary>
        public virtual void Cancel()
        {
            if (!EnsureUndisposed(throwException: false) || IsDiscarded)
                return;
            if (Cancellation is null)
                throw new InvalidOperationException();
            Logger?.Log(LogLevel.Debug, "{this} canceling", ToString());
            Cancellation.Cancel();
        }

        /// <summary>
        /// Cancel
        /// </summary>
        public virtual async Task CancelAsync()
        {
            if (!EnsureUndisposed(throwException: false) || IsDiscarded)
                return;
            if (Cancellation is null)
                throw new InvalidOperationException();
            Logger?.Log(LogLevel.Debug, "{this} canceling", ToString());
            await Cancellation.CancelAsync().DynamicContext();
        }

        /// <inheritdoc/>
        public override void SetScopeParameter(in IRpcScopeParameter? parameter)
        {
            EnsureUndisposed();
            EnsureNotDiscarded();
            RpcCancellationScopeParameter? cancellationParameter = parameter as RpcCancellationScopeParameter;
            if (parameter is not null && cancellationParameter is null)
                throw new ArgumentException($"{parameter.GetType()} isn't a {typeof(RpcCancellationScopeParameter)}", nameof(parameter));
            if (_ScopeParameter is not null)
                throw new InvalidOperationException("Scope parameter was set already");
            _ScopeParameter = parameter;
            if (cancellationParameter is not null)
                cancellationParameter.Scope ??= this;
        }

        /// <summary>
        /// Handle a cancellation
        /// </summary>
        protected virtual async void HandleCancellationAsync()
        {
            await Task.Yield();
            try
            {
                Registration?.Dispose();
                Logger?.Log(LogLevel.Debug, "{this} was canceled", ToString());
                WasCanceled = true;
                if (WasTriggered)
                {
                    await DiscardAsync().DynamicContext();
                    await DisposeAsync().DynamicContext();
                }
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} cancellation handling failed exceptional: {ex}", ToString(), ex);
                ErrorHandling.Handle(new($"{this} cancellation handling failed exceptional", ex, Processor.ErrorSource, this));
            }
        }

        /// <summary>
        /// Handle a trigger message (sent from the peer when the remote sope was stored)
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        protected virtual async Task HandleTriggerAsync(RemoteScopeTriggerMessage message, CancellationToken cancellationToken)
        {
            Logger?.Log(LogLevel.Debug, "{this} got a trigger from the remote scope", ToString());
            if (WasTriggered)
                throw new InvalidOperationException("Cancellation scope was triggered already");
            WasTriggered = true;
            if (WasCanceled)
            {
                await DiscardAsync(cancellationToken: cancellationToken).DynamicContext();
                await DisposeAsync().DynamicContext();
            }
        }

        /// <inheritdoc/>
        protected override async Task<bool> HandleMessageIntAsync(IRpcScopeMessage message, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case RemoteScopeTriggerMessage trigger:
                    await HandleTriggerAsync(trigger, cancellationToken).DynamicContext();
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Registration?.Dispose();
            base.Dispose(disposing);
            Cancellation?.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            Registration?.Dispose();
            await base.DisposeCore().DynamicContext();
            Cancellation?.Dispose();
        }

        /// <summary>
        /// Register this scope to <see cref="RpcScopes"/>
        /// </summary>
        public static void Register() => RpcScopes.RegisterScope(new()
        {
            Type = TYPE,
            LocalScopeType = typeof(RpcCancellationScope),
            RemoteScopeType = typeof(RpcCancellationRemoteScope),
            LocalScopeFactory = CreateAsync,
            RemoteScopeFactory = RpcCancellationRemoteScope.CreateAsync,
            LocalScopeParameterFactory = RpcCancellationScopeParameter.CreateAsync,
            LocalScopeValueType = typeof(RpcCancellationScopeValue),
            LocalScopeObjectTypes = [typeof(CancellationToken)],
            RemoteScopeObjectTypes = [typeof(CancellationToken)],
            ParameterLocalScopeFactory = (processor, value, ct) =>
            {
                if (value is not CancellationToken cancellation)
                    throw new InvalidProgramException($"{value?.GetType()} isn't a {typeof(CancellationToken)}");
                return Task.FromResult<RpcProcessor.RpcScopeBase?>(
                    new RpcCancellationScope(
                        processor,
                        new RpcCancellationScopeParameter()
                        {
                            Token = cancellation,
                            StoreScope = !cancellation.IsCancellationRequested,
                            DisposeScopeValue = false,
                            DisposeScopeValueOnError = false,
                            InformMasterWhenDisposing = !cancellation.IsCancellationRequested
                        }
                    )
                    );
            },
            ReturnLocalScopeFactory = (processor, method, value, ct) =>
            {
                if (value is not CancellationToken cancellation)
                    throw new InvalidProgramException($"{value?.GetType()} isn't a {typeof(CancellationToken)}");
                return Task.FromResult<RpcProcessor.RpcScopeBase?>(
                    new RpcCancellationScope(
                        processor,
                        new RpcCancellationScopeParameter()
                        {
                            Token = cancellation,
                            StoreScope = !cancellation.IsCancellationRequested,
                            DisposeScopeValue = false,
                            DisposeScopeValueOnError = false,
                            InformMasterWhenDisposing = !cancellation.IsCancellationRequested
                        }
                    )
                    );
            }
        });


        /// <summary>
        /// Create
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="parameter">RPC scope parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Scope</returns>
        public static Task<RpcProcessor.RpcScopeBase> CreateAsync(RpcProcessor processor, IRpcScopeParameter parameter, CancellationToken cancellationToken = default)
            => Task.FromResult<RpcProcessor.RpcScopeBase>(new RpcCancellationScope(processor, parameter.Key)
            {
                ScopeParameter = parameter as RpcCancellationScopeParameter 
                    ?? throw new InvalidProgramException($"{parameter.GetType()} isn't a {typeof(RpcCancellationScopeParameter)}")
            });
    }
}
