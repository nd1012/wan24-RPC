﻿using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

/*
 * The number of RPC calls from the peer is limited in two ways:
 * 
 * 1. The number of parallel executed API methods to protect CPU ressources
 * 2. The number of pending calls to protect memory ressources
 * 
 * The peer needs to know and respect these limits. A pending call queue overflow is a protocol error which disconnects the peer.
 */

namespace wan24.RPC.Processing
{
    // Call queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// Create a call queue
        /// </summary>
        /// <returns>Call queue</returns>
        protected virtual CallQueue CreateCallQueue() => new(this)
        {
            Name = "Incoming RPC calls"
        };

        /// <summary>
        /// RPC call queue
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        protected class CallQueue(in RpcProcessor processor)
            : ParallelItemQueueWorkerBase<Call>(processor.Options.CallQueue.Capacity, processor.Options.CallQueue.Threads)
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <summary>
            /// Logger
            /// </summary>
            public virtual ILogger? Logger => Processor.Logger;

            /// <inheritdoc/>
            protected override async Task ProcessItem(Call item, CancellationToken cancellationToken)
            {
                await Task.Yield();
                Logger?.Log(LogLevel.Debug, "{this} processing call #{id}", ToString(), item.Message.Id);
                if (item.Completion.Task.IsCompleted)
                {
                    Logger?.Log(LogLevel.Debug, "{this} call #{id} processed already", ToString(), item.Message.Id);
                    return;
                }
                if (item.Message is not RequestMessage request)
                {
                    Logger?.Log(LogLevel.Warning, "{this} call #{id} has no valid request message ({type} instead)", ToString(), item.Message.Id, item.Message.GetType());
                    if (!item.Completion.Task.IsCompleted)
                        item.Completion.TrySetException(new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)"));
                    return;
                }
                List<object?>? parameters = null;
                object? returnValue = null;
                try
                {
                    // Find the API method
                    if (await FindApiMethodAsync(item, request).DynamicContext() is not RpcApiMethodInfo method)
                    {
                        await Processor.OnCallErrorAsync(item, new InvalidDataException("API or method not found"), parameters, returnValue).DynamicContext();
                        return;
                    }
                    // Create a context
                    using Cancellations cancellation = new(cancellationToken, Processor.CancelToken, item.Cancellation.Token);
                    RpcContext context = Processor.CreateCallContext(request, method, cancellation);
                    await using (context.DynamicContext())
                    {
                        // Authorize
                        Logger?.Log(LogLevel.Trace, "{this} authorizing call #{id} API method \"{method}\" for the current context", ToString(), item.Id, method);
                        if (!await AuthorizeContextAsync(item, request, method, context).DynamicContext())
                        {
                            await Processor.OnCallErrorAsync(item, new UnauthorizedAccessException("Not authorized"), parameters, returnValue).DynamicContext();
                            return;
                        }
                        // Prepare DI
                        item.Context = context;
                        context.Services.AddDiObject(Processor);
                        context.Services.AddDiObject(context);
                        _ = context.Services.AddDiObject(cancellation.Cancellation);
                        // Prepare parameters
                        await request.DeserializeParametersAsync(method, cancellation).DynamicContext();
                        int index = 0;
                        parameters = new(method.Parameters.Count);
                        object? value;
                        foreach (RpcApiMethodParameterInfo p in method.Parameters.Values)
                        {
                            Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value", ToString(), item.Id, p.Method, p.Parameter.Name);
                            value = await GetParameterValueAsync(item, request, context, p, p.RPC ? index : -1).DynamicContext();
                            Logger?.Log(LogLevel.Trace, "{this} resolved call #{id} API method \"{method}\" parameter \"{name}\" value type {type}", ToString(), item.Id, p.Method, p.Parameter.Name, value?.GetType().ToString() ?? "NULL");
                            if (value is not null)
                            {
                                // Finalize the parameter value for the method call
                                Logger?.Log(LogLevel.Trace, "{this} resolving final call #{id} API method \"{method}\" parameter \"{name}\" value type {type}", ToString(), item.Id, p.Method, p.Parameter.Name, value?.GetType().ToString() ?? "NULL");
                                value = await GetFinalParameterValueAsync(item, request, context, p, p.RPC ? index : -1, value).DynamicContext();
                                Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{method}\" parameter \"{name}\" value type is now {type} after finalizing", ToString(), item.Id, p.Method, p.Parameter.Name, value?.GetType().ToString() ?? "NULL");
                            }
                            if (value is not null && !p.Parameter.ParameterType.IsAssignableFrom(value.GetType()))
                            {
                                await Processor.OnCallErrorAsync(
                                    item,
                                    new ArgumentException(
                                        $"RPC parameter \"{p.Parameter.Name}\" for API method \"{method}\" type is {value.GetType()} - {p.Parameter.ParameterType} expected",
                                        p.Parameter.Name
                                    ), 
                                    parameters, 
                                    returnValue
                                    ).DynamicContext();
                                return;
                            }
                            parameters.Add(value);
                            if (p.RPC)
                                index++;
                        }
                        // Call the method
                        Logger?.Log(LogLevel.Trace, "{this} processing call #{id} API method \"{method}\" execution", ToString(), item.Id, method);
                        item.WasProcessing = true;
                        returnValue = method.Method.Invoke(method.Method.IsStatic ? null : method.API.Instance, [.. parameters]);
                        while (returnValue is Task task)
                        {
                            await task.DynamicContext();
                            returnValue = task.GetType().IsGenericType
                                ? task.GetResultNullable<object?>()
                                : null;
                        }
                        item.DidReturn = true;
                        Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{method}\" finished execution with return value type {type}", ToString(), item.Id, method, returnValue?.GetType().ToString() ?? "NULL");
                        await Processor.OnCallExecutedAsync(item, parameters).DynamicContext();
                        // Handle the response
                        if (returnValue is not null)
                        {
                            // Finalize the return value for sending
                            Logger?.Log(LogLevel.Trace, "{this} finalizing call #{id} API method \"{method}\" return value type {type}", ToString(), item.Id, method, returnValue?.GetType().ToString() ?? "NULL");
                            returnValue = await GetFinalReturnValueAsync(item, request, context, method, returnValue).DynamicContext();
                            Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{method}\" return value type is now {type} after finalizing", ToString(), item.Id, method, returnValue?.GetType().ToString() ?? "NULL");
                        }
                        item.Completion.TrySetResult(request.WantsReturnValue ? returnValue : null);
                    }
                }
                catch (Exception ex)
                {
                    await Processor.OnCallErrorAsync(item, ex, parameters, returnValue).DynamicContext();
                }
                finally
                {
                    item.SetDone();
                }
            }

            /// <summary>
            /// Find the API method to use (should set a <see cref="Call.Completion"/> exception on error)
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <returns>API method (validated to be callable using the RPC request informations)</returns>
            protected virtual Task<RpcApiMethodInfo?> FindApiMethodAsync(Call item, RequestMessage request)
            {
                // Find the API method
                RpcApiMethodInfo? res = request.Api is not null
                    ? Processor.Options.API.FindApi(request.Api)?.FindMethod(request.Method)
                    : Processor.Options.API.FindApiMethod(request.Method);
                if (res is null)
                {
                    item.Completion.TrySetException(new InvalidDataException("API or method not found"));
                    return Task.FromResult(res);
                }
                // Handle API method versioning for the current context
                RpcApiMethodInfo firstMethod = res,
                    currentMethod;
                string? newerMethodName;
                HashSet<RpcApiMethodInfo> seen = [res];
                while (true)
                    try
                    {
                        // Get the newer method version name
                        currentMethod = res;
                        newerMethodName = res.Version is null
                            ? res.Name
                            : res.Version.GetNewerMethodName(Processor.Options.RpcVersion, res);
                        if (newerMethodName == res.Name)
                            break;
                        // Handle an incompatibility (peer needs to use a newer RPC protocol version in order to be able to call the method)
                        if (newerMethodName is null)
                        {
                            item.Completion.TrySetException(
                                new InvalidOperationException(
                                    $"Peer API version #{Processor.Options.RpcVersion} is incompatible with the methods \"{res}\" API version requirement \"{res.Version}\""
                                    )
                                );
                            return Task.FromResult<RpcApiMethodInfo?>(null);
                        }
                        // Get the newer method
                        Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{currentMethod}\" forwards to \"{name}\"", ToString(), item.Id, currentMethod, newerMethodName);
                        res = res.API.FindMethod(newerMethodName);
                        if (res is null)
                        {
                            item.Completion.TrySetException(
                                new InvalidProgramException(
                                    $"API method \"{currentMethod}\" forwarded peer API version #{Processor.Options.RpcVersion} request to \"{newerMethodName}\", which is an unknown method name"
                                    )
                                );
                            return Task.FromResult(res);
                        }
                        // Avoid forward recursion
                        if (!seen.Add(res))
                        {
                            item.Completion.TrySetException(
                                new InvalidProgramException(
                                    $"API method \"{firstMethod}\" API version #{Processor.Options.RpcVersion} forwarding recursion (\"{string.Join("\"->\"", seen.Select(m => m.Name))}\"->\"{res.Name}\")"
                                    )
                                );
                            return Task.FromResult<RpcApiMethodInfo?>(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Completion.TrySetException(ex);
                        return Task.FromResult<RpcApiMethodInfo?>(null);
                    }
                // Handle an invalid parameter count
                if (request.Parameters is not null && request.Parameters.Length > res.RpcParameters.Count)
                {
                    item.Completion.TrySetException(new ArgumentException("API method parameter count mismatch"));
                    return Task.FromResult<RpcApiMethodInfo?>(null);
                }
                return Task.FromResult<RpcApiMethodInfo?>(res);
            }

            /// <summary>
            /// Authorize the RPC context (should set a <see cref="Call.Completion"/> exception on error)
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <param name="method">RPC API method</param>
            /// <param name="context">Context</param>
            /// <returns>If authorized</returns>
            protected virtual async Task<bool> AuthorizeContextAsync(Call item, RequestMessage request, RpcApiMethodInfo method, RpcContext context)
            {
                // Handle authorize all
                if (method.API.Authorize || method.Authorize)
                {
                    Logger?.Log(LogLevel.Trace, "{this} call #{id} API or method \"{method}\" authorizes all", ToString(), item.Id, method);
                    return true;
                }
                // Perform authorization
                try
                {
                    foreach (RpcAuthorizationAttributeBase authZ in method.API.Authorization.Concat(method.Authorization))
                        if (!await authZ.IsContextAuthorizedAsync(context).DynamicContext())
                        {
                            if (RpcContext.UnauthorizedHandler is RpcContext.Unauthorized_Delegate handler)
                                await handler(context, authZ).DynamicContext();
                            item.Completion.TrySetException(new UnauthorizedAccessException($"{authZ} denied API method \"{method}\" authorization for the current context"));
                            return false;
                        }
                        else
                        {
                            Logger?.Log(LogLevel.Trace, "{this} call #{id} API or method \"{method}\" authorized by {authZ}", ToString(), item.Id, method, authZ);
                        }
                    return true;
                }
                catch (Exception ex)
                {
                    item.Completion.TrySetException(ex);
                    return false;
                }
            }

            /// <summary>
            /// Get a parameter value for the API method call
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <param name="context">Context</param>
            /// <param name="parameter">API method parameter</param>
            /// <param name="index">Zero based index of the RPC call parameter (or <c>-1</c>, if it's not a RPC servable parameter)</param>
            /// <returns>Parameter value to use</returns>
            protected virtual async Task<object?> GetParameterValueAsync(
                Call item,
                RequestMessage request,
                RpcContext context,
                RpcApiMethodParameterInfo parameter,
                int index
                )
            {
                // Get given RPC parameter value
                if (parameter.RPC && request.Parameters is not null && index < request.Parameters.Length)
                {
                    if (request.Parameters[index] is null && !parameter.Nullable)
                        throw new ArgumentNullException(parameter.Parameter.Name, $"RPC parameter must not be NULL");
                    Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from RPC request parameter #{index} ({type})", ToString(), item.Id, parameter.Method, parameter.Parameter.Name, index, request.Parameters[index]?.GetType().ToString() ?? "NULL");
                    return request.Parameters[index];
                }
                // Try the scope
                if (parameter.Scope is not null)// Request the local scope first
                    if(Processor.GetScope(parameter.Scope.Key) is not RpcScopeBase scope)
                    {
                        if(parameter.Scope.ThrowOnMissingScope)
                            throw new ArgumentNullException(parameter.Parameter.Name, $"Required keyed scope \"{parameter.Scope.Key}\" not found");
                    }
                    else if (scope.Value is object scopeValue)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from scope #{scope} ({scopeType}) value {valueType}", ToString(), item.Id, parameter.Method, parameter.Parameter.Name, scope.Id, scope.GetType(), scopeValue.GetType());
                        return scopeValue;
                    }
                if (parameter.RemoteScope is not null)// Remote scope as 2nd source for the value
                    if (Processor.GetRemoteScope(parameter.RemoteScope.Key) is not RpcRemoteScopeBase scope)
                    {
                        if (parameter.RemoteScope.ThrowOnMissingScope)
                            throw new ArgumentNullException(parameter.Parameter.Name, $"Required keyed remote scope \"{parameter.RemoteScope.Key}\" not found");
                    }
                    else if (scope.Value is object scopeValue)
                    {
                        Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from remote scope #{scope} ({scopeType}) value {valueType}", ToString(), item.Id, parameter.Method, parameter.Parameter.Name, scope.Id, scope.GetType(), scopeValue.GetType());
                        return scopeValue;
                    }
                // Try DI
                ITryAsyncResult result = await context.Services.GetDiObjectAsync(parameter.Parameter.ParameterType, cancellationToken: context.Cancellation).DynamicContext();
                if (result.Succeed)
                    if (result.Result is null && !parameter.Nullable)
                    {
                        if (!parameter.Parameter.HasDefaultValue)
                            throw new ArgumentNullException(parameter.Parameter.Name, "DI resolved non-nullable parameter to NULL");
                    }
                    else
                    {
                        Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from DI ({type})", ToString(), item.Id, parameter.Method, parameter.Parameter.Name, result.Result?.GetType().ToString() ?? "NULL");
                        return result.Result;
                    }
                // Use the default value
                if (parameter.Parameter.HasDefaultValue)
                {
                    Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from default ({type})", ToString(), item.Id, parameter.Method, parameter.Parameter.Name, parameter.Parameter.DefaultValue?.GetType().ToString() ?? "NULL");
                    return parameter.Parameter.DefaultValue;
                }
                // Handle nullable parameter
                if (parameter.Nullable)
                {
                    Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" nullable value to NULL", ToString(), item.Id, parameter.Method, parameter.Parameter.Name);
                    return null;
                }
                // Fail, if a required parameter value can't be resolved
                throw new ArgumentNullException(parameter.Parameter.Name, $"Can't get required parameter value \"{parameter.Method} -> {parameter.Parameter.Name}\" ({parameter.Parameter.ParameterType})");
            }

            /// <summary>
            /// Get the final parameter value for the API method call
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <param name="context">Context</param>
            /// <param name="parameter">API method parameter</param>
            /// <param name="index">Zero based index of the RPC call parameter (or <c>-1</c>, if it's not a RPC servable parameter)</param>
            /// <param name="value">Current parameter value</param>
            /// <returns>Parameter value to use</returns>
            protected virtual async Task<object?> GetFinalParameterValueAsync(
                Call item,
                RequestMessage request,
                RpcContext context,
                RpcApiMethodParameterInfo parameter,
                int index,
                object? value
                )
            {
                // Scope parameter handling
                if(value is RpcScopeValue scopeValue)
                {
                    if (!RpcScopes.RemoteFactories.TryGetValue(scopeValue.Type, out RpcScopes.RemoteScopeFactory_Delegate? scopeFactory))
                        throw new ArgumentException("Unsupported scope type in parameter value", parameter.Parameter.Name);
                    RpcRemoteScopeBase? remoteScope = null;
                    try
                    {
                        remoteScope = await scopeFactory.Invoke(Processor, scopeValue, CancelToken).DynamicContext();
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Scope factory failed to create a remote scope from the parameter value", parameter.Parameter.Name, ex);
                    }
                    item.ParameterScopes.Add(remoteScope);
                    value = remoteScope.Value;
                }
                return value;
            }

            /// <summary>
            /// Get the final return value of a method request which will be sent back to the peer
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <param name="context">Context</param>
            /// <param name="method">API method</param>
            /// <param name="returnValue">Return value</param>
            /// <returns>Final return value</returns>
            protected virtual async Task<object?> GetFinalReturnValueAsync(
                Call item,
                RequestMessage request,
                RpcContext context,
                RpcApiMethodInfo method,
                object? returnValue
                )
            {
                // Scope handling
                if (returnValue is not null && RpcScopes.GetReturnScopeFactory(returnValue.GetType()) is RpcScopes.ReturnScopeFactory_Delegate scopeFactory)
                    if(await scopeFactory.Invoke(Processor, returnValue, CancelToken).DynamicContext() is RpcScopeBase scope)
                    {
                        item.ReturnScope = scope;
                        returnValue = scope.Value;
                    }
                    else
                    {
                        await returnValue.TryDisposeAsync().DynamicContext();
                        returnValue = null;
                    }
                return returnValue;
            }
        }
    }
}
