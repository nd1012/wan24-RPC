﻿using System.Reflection;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection.Extensions;

namespace wan24.RPC.Api.Reflection
{
    /// <summary>
    /// RPC API method parameter info
    /// </summary>
    public class RpcApiMethodParameterInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="pi">Parameter</param>
        /// <param name="index">Index</param>
        public RpcApiMethodParameterInfo(in RpcApiMethodInfo method, in ParameterInfo pi, in int index)
        {
            Method = method;
            Parameter = pi;
            Index = index;
            RPC = pi.GetCustomAttributeCached<NoRpcAttribute>() is null;
            Enumerable = pi.ParameterType.IsEnumerable(strict: true, asyncOnly: true);
            Stream = pi.GetCustomAttributeCached<RpcStreamAttribute>();
            DisposeParameterValue = pi.GetCustomAttributeCached<NoRpcDisposeAttribute>() is null;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcApiMethodParameterInfo() { }

        /// <summary>
        /// API
        /// </summary>
        public RpcApiInfo API => Method.API;

        /// <summary>
        /// Method
        /// </summary>
        public RpcApiMethodInfo Method { get; protected set; } = null!;

        /// <summary>
        /// Parameter
        /// </summary>
        public ParameterInfo Parameter { get; protected set; } = null!;

        /// <summary>
        /// Index
        /// </summary>
        public int Index { get; protected set; }

        /// <summary>
        /// If the parameter is available via RPC
        /// </summary>
        public bool RPC { get; protected set; } = true;

        /// <summary>
        /// If the parameter value may be transported as RPC enumerable
        /// </summary>
        public bool Enumerable { get; protected set; }

        /// <summary>
        /// Stream configuration
        /// </summary>
        public RpcStreamAttribute? Stream { get; protected set; }

        /// <summary>
        /// If to dispose the parameter value after processing
        /// </summary>
        public bool DisposeParameterValue { get; protected set; } = true;
    }
}