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
                if (item.Completion.Task.IsCompleted)
                    return;
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
                        if (!item.Completion.Task.IsCompleted)
                            item.Completion.TrySetException(new InvalidDataException("API or method not found"));
                        return;
                    }
                    // Create a context
                    using Cancellations cancellation = new(cancellationToken, item.ProcessorCancellation, item.CallCancellation.Token);
                    RpcContext context = Processor.GetContext(request, method, cancellation);
                    await using (context.DynamicContext())
                    {
                        // Authorize
                        if (!await AuthorizeAsync(item, request, method, context).DynamicContext())
                        {
                            if (!item.Completion.Task.IsCompleted)
                                item.Completion.TrySetException(new UnauthorizedAccessException("Not authorized"));
                            return;
                        }
                        // Prepare the method call
                        item.Context = context;
                        context.Services.AddDiObject(context);
                        context.Services.AddDiObject(Processor);
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
                        returnValue = typeof(Task).IsAssignableFrom(method.Method.ReturnType)
                            ? await method.Method.InvokeAutoAsync(method.Method.IsStatic ? null : method.API.Instance, parameters).DynamicContext()
                            : method.Method.InvokeAuto(method.Method.IsStatic ? null : method.API.Instance, parameters);
                        while (returnValue is Task task)
                        {
                            await task.DynamicContext();
                            returnValue = task.GetType().IsGenericType
                                ? task.GetResultNullable<object?>()
                                : null;
                        }
                        // Handle the response
                        item.SetDone();
                        item.Completion.TrySetResult(await GetReturnValueAsync(item, request, context, method, returnValue).DynamicContext());
                    }
                }
                catch (Exception ex)
                {
                    if (returnValue is not null && method!.DisposeReturnValue)
                        await returnValue.TryDisposeAsync().DynamicContext();
                    item.Completion.TrySetException(ex);
                    return;
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
                if (request.Parameters is not null && request.Parameters.Length > res.Parameters.Count)
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
            protected virtual async Task<bool> AuthorizeAsync(Call item, RequestMessage request, RpcApiMethodInfo method, RpcContext context)
            {
                if (method.API.Authorize || method.Authorize)
                    return true;
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
                //TODO Streams and enumerations
                if (parameter.RPC && request.Parameters is not null && index < request.Parameters.Length)
                    return request.Parameters[index];
                ITryAsyncResult result = await context.Services.GetDiObjectAsync(parameter.Parameter.ParameterType, cancellationToken: context.Cancellation).DynamicContext();
                if (result.Succeed)
                    return result.Result;
                if (parameter.Parameter.HasDefaultValue)
                    return parameter.Parameter.DefaultValue;
                if (parameter.Nullable)
                    return null;
                throw new ArgumentException("Can't get required parameter value", parameter.Parameter.Name);
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
            protected virtual Task<object?> GetReturnValueAsync(
                Call item, 
                RequestMessage request, 
                RpcContext context, 
                RpcApiMethodInfo method, 
                object? returnValue
                )
            {
                if (returnValue is null)
                    return Task.FromResult(returnValue);
                //TODO Stream and enumeration
                return Task.FromResult<object?>(returnValue);
            }
        }
    }
}
