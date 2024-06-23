using Microsoft.Extensions.Logging;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    // Call handling events
    public partial class RpcProcessor
    {
        /// <summary>
        /// Handle a call processing error (parameter and return value handling; this method should not throw)
        /// </summary>
        /// <param name="call">RPC call</param>
        /// <param name="error">Error</param>
        /// <param name="parameters">API method parameters</param>
        /// <param name="returnValue">Return value</param>
        protected virtual async Task OnCallErrorAsync(Call call, Exception error, List<object?>? parameters, object? returnValue)
        {
            RequestMessage message = (RequestMessage)call.Message;
            async Task DisposeOnlyAsync()
            {
                if (parameters is not null)
                    foreach (object? parameter in parameters)
                        if (parameter is not null)
                            await parameter.TryDisposeAsync().DynamicContext();
                if (message.Parameters is not null)
                    foreach (object? parameter in message.Parameters)
                        if (parameter is not null)
                            await parameter.TryDisposeAsync().DynamicContext();
                if (returnValue is not null)
                    await returnValue.TryDisposeAsync().DynamicContext();
            }
            bool willDisconnect = !call.WasProcessing || 
                call.DidReturn ||
                Options.DisconnectOnApiError || 
                (call.Context?.Method?.API?.DisconnectOnError ?? call.Context?.Method?.DisconnectOnError ?? false);
            try
            {
                if (willDisconnect)
                {
                    await DisposeOnlyAsync().DynamicContext();
                    return;
                }
                // Handle parameters
                if (parameters is not null)
                {
                    // Handle RPC parameters of the message and the method call
                    Contract.Assume(call.Context?.Method is not null);
                    RpcApiMethodParameterInfo parameter;
                    for (int i = 0, len = call.Context.Method.RpcParameters.Count; i < len; i++)
                    {
                        parameter = call.Context.Method.RpcParameters.Items[i];
                        if (
                            parameter.Index < parameters.Count &&
                            parameters[parameter.Index] is object value &&
                            (!call.WasProcessing || parameter.DisposeParameterValue || parameter.DisposeParameterValueOnError)
                            )
                            await HandleValueOnErrorAsync(value, outgoing: false, error).DynamicContext();
                        if (message.Parameters is not null && i < message.Parameters.Length && message.Parameters[i] is object value2)
                            await HandleValueOnErrorAsync(value2, outgoing: false, error).DynamicContext();
                    }
                }
                else if (message.Parameters is not null)
                {
                    // Handle RPC parameters of the message
                    await HandleValuesOnErrorAsync(outgoing: false, error, message.Parameters).DynamicContext();
                }
                // Handle the return value
                if (returnValue is not null && (call.Context?.Method?.DisposeReturnValue ?? call.Context?.Method?.DisposeReturnValueOnError ?? true))
                    await HandleValueOnErrorAsync(returnValue, outgoing: true, error).DynamicContext();
                // Handle remote scopes
                foreach (RpcRemoteScopeBase remoteScope in call.ParameterScopes)
                    if (remoteScope.Parameter?.DisposeParameterValue ?? remoteScope.Parameter?.DisposeParameterValueOnError ?? true)
                    {
                        await remoteScope.SetIsErrorAsync(error).DynamicContext();
                        await remoteScope.DisposeAsync().DynamicContext();
                    }
                // Handle scope return value
                if (call.ReturnScope is not null && (call.ReturnScope.Method?.DisposeReturnValue ?? call.ReturnScope.Method?.DisposeReturnValueOnError ?? true))
                {
                    await call.ReturnScope.SetIsErrorAsync(error).DynamicContext();
                    await call.ReturnScope.DisposeAsync().DynamicContext();
                }
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} failed to handle call #{id} error (will disconnect): {ex}", ToString(), call.Id, ex);
                if (!willDisconnect)
                    try
                    {
                        await DisposeOnlyAsync().DynamicContext();
                    }
                    catch (Exception ex2)
                    {
                        Logger?.Log(LogLevel.Error, "{this} failed to handle call #{id} error (disposing values failed): {ex}", ToString(), call.Id, ex2);
                    }
                _ = StopExceptionalAndDisposeAsync(ex);
            }
            finally
            {
                if (!call.Completion.Task.IsCompleted)
                    call.Completion.TrySetException(error);
            }
        }

        /// <summary>
        /// Handle an executed call (parameter handling)
        /// </summary>
        /// <param name="call">RPC call</param>
        /// <param name="parameters">API method parameters</param>
        protected virtual async Task OnCallExecutedAsync(Call call, List<object?> parameters)
        {
            Contract.Assume(call.Context?.Method is not null);
            RequestMessage message = (RequestMessage)call.Message;
            RpcApiMethodParameterInfo parameter;
            for (int i = 0, len = call.Context.Method.RpcParameters.Count; i < len; i++)
            {
                parameter = call.Context.Method.RpcParameters.Items[i];
                if (!parameter.DisposeParameterValue)
                    continue;
                if (parameter.Index < parameters.Count && parameters[parameter.Index] is object value)
                    await value.TryDisposeAsync().DynamicContext();
                if (message.Parameters is not null && i < message.Parameters.Length && message.Parameters[i] is object value2)
                    await value2.TryDisposeAsync().DynamicContext();
            }
            foreach (RpcRemoteScopeBase remoteScope in call.ParameterScopes)
                if (remoteScope.Parameter?.DisposeParameterValue ?? true)
                    await remoteScope.DisposeAsync().DynamicContext();
        }

        /// <summary>
        /// Handle a responded call
        /// </summary>
        /// <param name="call">RPC call</param>
        /// <param name="returnValue">Return value</param>
        protected virtual async Task OnCallRespondedAsync(Call call, object? returnValue)
        {
            if (returnValue is null)
                return;
            Contract.Assume(call.Context?.Method is not null);
            if (!call.Context.Method.DisposeReturnValue)
                return;
            await returnValue.TryDisposeAsync().DynamicContext();
        }
    }
}
