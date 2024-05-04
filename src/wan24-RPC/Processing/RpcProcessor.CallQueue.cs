using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Api.Reflection.Extensions;

namespace wan24.RPC.Processing
{
    // Call queue
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC call queue
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="processor">RPC processor</param>
        protected class CallQueue(in RpcProcessor processor)
            : ParallelItemQueueWorkerBase<Call>(processor.Options.CallQueueSize, processor.Options.CallThreads)
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public RpcProcessor Processor { get; } = processor;

            /// <inheritdoc/>
            protected override async Task ProcessItem(Call item, CancellationToken cancellationToken)
            {
                await Task.Yield();
                Processor.Options.Logger?.Log(LogLevel.Debug, "{this} processing call #{id}", this, item.Message.Id);
                if (item.Completion.Task.IsCompleted)
                {
                    Processor.Options.Logger?.Log(LogLevel.Debug, "{this} call #{id} processed already", this, item.Message.Id);
                    return;
                }
                if (item.Message is not RequestMessage request)
                {
                    item.Completion.TrySetException(new InvalidDataException($"Request message expected (got {item.Message.GetType()} instead)"));
                    return;
                }
                RpcApiMethodInfo? method = null;
                object? returnValue = null;
                try
                {
                    // Find the API method
                    method = await FindApiMethodAsync(item, request).DynamicContext();
                    if (method is null)
                    {
                        item.Completion.TrySetException(new InvalidDataException("API or method not found"));
                        return;
                    }
                    // Create a context
                    using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.CallCancellation.Token);
                    RpcContext context = Processor.GetContext(request, method, cancellation);
                    await using (context.DynamicContext())
                    {
                        // Authorize
                        if (!await AuthorizeContextAsync(item, request, method, context).DynamicContext())
                        {
                            item.Completion.TrySetException(new UnauthorizedAccessException("Not authorized"));
                            return;
                        }
                        // Prepare DI
                        item.Context = context;
                        context.Services.AddDiObject(Processor);
                        context.Services.AddDiObject(context);
                        _ = context.Services.AddDiObject(cancellation.Cancellation);
                        // Prepare parameters
                        int index = 0;
                        List<object?> parameters = new(method.Parameters.Count);
                        foreach (RpcApiMethodParameterInfo p in method.Parameters.Values)
                        {
                            parameters.Add(await GetParameterValueAsync(item, request, context, p, p.RPC ? index : -1).DynamicContext());
                            if (p.RPC)
                                index++;
                        }
                        // Call the method
                        item.Processed = true;
                        returnValue = method.Method.Invoke(method.Method.IsStatic ? null : method.API.Instance, [.. parameters]);
                        while (returnValue is Task task)
                        {
                            await task.DynamicContext();
                            returnValue = task.GetType().IsGenericType
                                ? task.GetResultNullable<object?>()
                                : null;
                        }
                        // Handle the response
                        item.SetDone();
                        item.Completion.TrySetResult(
                            request.WantsReturnValue
                                ? await GetFinalReturnValueAsync(item, request, context, method, returnValue).DynamicContext()
                                : null
                                );
                    }
                }
                catch (Exception ex)
                {
                    if (returnValue is not null && method!.DisposeReturnValue)
                        await returnValue.TryDisposeAsync().DynamicContext();
                    item.Completion.TrySetException(ex);
                    return;
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
                RpcApiMethodInfo? res = request.Api is not null
                    ? Processor.Options.API.FindApi(request.Api)?.FindMethod(request.Method)
                    : Processor.Options.API.FindApiMethod(request.Method);
                if (res is null)
                {
                    Processor.Options.Logger?.Log(LogLevel.Warning, "{this} call #{id} API \"{api}\" method \"{method}\" not found", this, item.Message.Id, request.Api, request.Method);
                    item.Completion.TrySetException(new InvalidDataException("API or method not found"));
                    return Task.FromResult(res);
                }
                RpcApiMethodInfo firstMethod = res,
                    currentMethod;
                string? newerMethodName;
                HashSet<RpcApiMethodInfo> seen = [res];
                while (true)
                    try
                    {
                        currentMethod = res;
                        newerMethodName = res.Version is null
                            ? res.Name
                            : res.Version.GetNewerMethodName(Processor.Options.ApiVersion, res);
                        if (newerMethodName == res.Name)
                            break;
                        if (newerMethodName is null)
                        {
                            item.Completion.TrySetException(
                                new InvalidOperationException(
                                    $"Peer API version #{Processor.Options.ApiVersion} is incompatible with the methods \"{res}\" API version requirement \"{res.Version}\""
                                    )
                                );
                            return Task.FromResult<RpcApiMethodInfo?>(null);
                        }
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{currentMethod}\" forwards to \"{name}\"", this, item.Message.Id, currentMethod, newerMethodName);
                        res = res.API.FindMethod(newerMethodName);
                        if (res is null)
                        {
                            item.Completion.TrySetException(
                                new InvalidProgramException(
                                    $"API method \"{currentMethod}\" forwarded peer API version #{Processor.Options.ApiVersion} request to \"{newerMethodName}\", which is an unknown method name"
                                    )
                                );
                            return Task.FromResult(res);
                        }
                        if (!seen.Add(res))
                        {
                            item.Completion.TrySetException(
                                new InvalidProgramException(
                                    $"API method \"{firstMethod}\" API version #{Processor.Options.ApiVersion} forwarding recursion (\"{string.Join("\"->\"", seen.Select(m => m.Name))}\"->\"{res.Name}\")"
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
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} authorizing call #{id} API method \"{method}\" for the current context", this, item.Message.Id, method);
                if (method.API.Authorize || method.Authorize)
                {
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} API or method \"{method}\" authorizes all", this, item.Message.Id, method);
                    return true;
                }
                try
                {
                    foreach (RpcAuthorizationAttributeBase authZ in method.API.Authorization.Concat(method.Authorization))
                        if (!await authZ.IsContextAuthorizedAsync(context))
                        {
                            item.Completion.TrySetException(
                                new UnauthorizedAccessException($"{authZ} denied API method \"{method}\" authorization for the current context")
                                );
                            return false;
                        }
                        else
                        {
                            Processor.Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} API or method \"{method}\" authorized by {authZ}", this, item.Message.Id, method, authZ);
                        }
                }
                catch (Exception ex)
                {
                    item.Completion.TrySetException(ex);
                    return false;
                }
                return true;
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
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value", this, item.Message.Id, parameter.Method, parameter.Parameter.Name);
                object? res = null;
                if (parameter.RPC && request.Parameters is not null && index < request.Parameters.Length)
                {
                    if (request.Parameters[index] is null)
                    {
                        if (!parameter.Nullable)
                            throw new ArgumentException($"RPC parameter must not be NULL", parameter.Parameter.Name);
                    }
                    else if (!parameter.Parameter.ParameterType.IsAssignableFrom(request.Parameters[index]!.GetType()))
                    {
                        throw new ArgumentException($"RPC parameter type is {request.Parameters[index]!.GetType()} - {parameter.Parameter.ParameterType} expected", parameter.Parameter.Name);
                    }
                    Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from RPC request parameter #{index} ({type})", this, item.Message.Id, parameter.Method, parameter.Parameter.Name, index, request.Parameters[index]?.GetType().ToString() ?? "NULL");
                    res = request.Parameters[index];
                }
                else
                {
                    ITryAsyncResult result = await context.Services.GetDiObjectAsync(parameter.Parameter.ParameterType, cancellationToken: context.Cancellation).DynamicContext();
                    if (result.Succeed)
                    {
                        if (result.Result is null)
                        {
                            if (!parameter.Nullable)
                                throw new ArgumentException($"DI resolved non-nullable parameter to NULL", parameter.Parameter.Name);
                        }
                        else if (!parameter.Parameter.ParameterType.IsAssignableFrom(result.Result!.GetType()))
                        {
                            throw new ArgumentException($"DI resolved to {result.Result!.GetType()} - {parameter.Parameter.ParameterType} expected", parameter.Parameter.Name);
                        }
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from DI ({type})", this, item.Message.Id, parameter.Method, parameter.Parameter.Name, result.Result?.GetType().ToString() ?? "NULL");
                        res = result.Result;
                    }
                    else if (parameter.Parameter.HasDefaultValue)
                    {
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" value from default ({type})", this, item.Message.Id, parameter.Method, parameter.Parameter.Name, parameter.Parameter.DefaultValue?.GetType().ToString() ?? "NULL");
                        res = parameter.Parameter.DefaultValue;
                    }
                    else if (parameter.Nullable)
                    {
                        Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving call #{id} API method \"{method}\" parameter \"{name}\" nullable value to NULL", this, item.Message.Id, parameter.Method, parameter.Parameter.Name);
                    }
                    else
                    {
                        throw new ArgumentException($"Can't get required parameter value \"{parameter.Method} -> {parameter.Parameter.Name}\" ({parameter.Parameter.ParameterType})", parameter.Parameter.Name);
                    }
                }
                return await GetFinalParameterValueAsync(item, request, context, parameter, index, res).DynamicContext();
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
            protected virtual Task<object?> GetFinalParameterValueAsync(
                Call item,
                RequestMessage request,
                RpcContext context,
                RpcApiMethodParameterInfo parameter,
                int index,
                object? value
                )
            {
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} resolving final call #{id} API method \"{method}\" parameter \"{name}\" value type {type}", this, item.Message.Id, parameter.Method, parameter.Parameter.Name, value?.GetType().ToString() ?? "NULL");
                if (value is null)
                    return Task.FromResult(value);
                //TODO Streams and enumerations
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{method}\" parameter \"{name}\" value type is now {type} after finalizing", this, item.Message.Id, parameter.Method, parameter.Parameter.Name, value?.GetType().ToString() ?? "NULL");
                return Task.FromResult(value);
            }

            /// <summary>
            /// Get the final return value of a method callwhich will be sent back to the peer
            /// </summary>
            /// <param name="item">RPC call</param>
            /// <param name="request">RPC request message</param>
            /// <param name="context">Context</param>
            /// <param name="method">API method</param>
            /// <param name="returnValue">Return value</param>
            /// <returns>Final return value</returns>
            protected virtual Task<object?> GetFinalReturnValueAsync(
                Call item,
                RequestMessage request,
                RpcContext context,
                RpcApiMethodInfo method,
                object? returnValue
                )
            {
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} finalizing call #{id} API method \"{method}\" return value type {type}", this, item.Message.Id, method, returnValue?.GetType().ToString() ?? "NULL");
                if (returnValue is null)
                    return Task.FromResult(returnValue);
                //TODO Stream and enumeration
                Processor.Options.Logger?.Log(LogLevel.Trace, "{this} call #{id} API method \"{method}\" return value type is now {type} after finalizing", this, item.Message.Id, method, returnValue?.GetType().ToString() ?? "NULL");
                return Task.FromResult(returnValue);
            }
        }
    }
}
