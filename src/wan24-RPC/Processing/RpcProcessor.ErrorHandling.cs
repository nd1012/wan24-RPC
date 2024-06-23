using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Error handling
    public partial class RpcProcessor
    {
        /// <summary>
        /// Handle parameter or return values on error (this method shouldn't throw)
        /// </summary>
        /// <param name="outgoing">If the values were going to be sent to the peer</param>
        /// <param name="ex">Exception</param>
        /// <param name="values">Values (will be disposed)</param>
        protected virtual async Task HandleValuesOnErrorAsync(bool outgoing, Exception ex, params object?[] values)
        {
            foreach (object? value in values)
                if (value is not null)
                    await HandleValueOnErrorAsync(value, outgoing, ex).DynamicContext();
        }

        /// <summary>
        /// Handle a parameter or return value on error (this method shouldn't throw)
        /// </summary>
        /// <param name="value">Value (will be disposed)</param>
        /// <param name="outgoing">If the value was going to be sent to the peer</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleValueOnErrorAsync(object value, bool outgoing, Exception ex)//TODO Separate method for parameter and return values?
        {
            bool res = true;
            // Special type handling
            try
            {
                switch (value)
                {
                    case IRpcScopeParameter scopeParameter:
                        {
                            await scopeParameter.SetIsErrorAsync().DynamicContext();
                            if (scopeParameter.Value is not null && RemoveScope(scopeParameter.Value.Id) is RpcScopeBase scope)
                                try
                                {
                                    await scope.SetIsErrorAsync(ex).DynamicContext();
                                }
                                catch
                                {
                                    res = false;
                                }
                                finally
                                {
                                    await scope.DisposeAsync().DynamicContext();
                                }
                        }
                        break;
                    case RpcScopeValue scopeValue:
                        {
                            if (outgoing && RemoveScope(scopeValue.Id) is RpcScopeBase scope)
                                try
                                {
                                    await scope.SetIsErrorAsync(ex).DynamicContext();
                                }
                                catch
                                {
                                    res = false;
                                }
                                finally
                                {
                                    value = scope;
                                }
                        }
                        break;
                    case IAsyncDisposable:
                    case IDisposable:
                        break;
                    default:
                        res = false;
                        break;
                }
            }
            catch
            {
                res = false;
            }
            // Additional disposable handling
            try
            {
                switch (value)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().DynamicContext();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch
            {
                res = false;
            }
            return res;
        }

        /// <summary>
        /// Handle an invalid response return value (this method shouldn't throw)
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleInvalidResponseReturnValueAsync(ResponseMessage message)
        {
            if (message.ReturnValue is null)
                return false;
            bool res = true;
            try
            {
                if(message.ReturnValue is RpcScopeValue scopeValue && RemoveRemoteScope(scopeValue.Id) is RpcRemoteScopeBase remoteScope)
                {
                    Logger?.Log(LogLevel.Debug, "{this} disposing invalid remote scope #{scope} return value for request #{id}", ToString(), scopeValue.Id, message.Id);
                    try
                    {
                        await remoteScope.SetIsErrorAsync(new InvalidOperationException("Invalid/unexpected return value")).DynamicContext();
                    }
                    catch
                    {
                        res = false;
                    }
                    finally
                    {
                        await remoteScope.DisposeAsync().DynamicContext();
                    }
                }
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} failed to handle invalid return value for request #{id}: {ex}", ToString(), message.Id, ex);
                res = false;
            }
            return res;
        }
    }
}
