# wan24-RPC

This library contains some RPC helper which enables an app to use any 
bi-directional RPC stream on the fly. It supports 

- Stream channels
- Enumeration channels
- Events
- Compression

## How to get it

There are two NuGet packages which you might want to use:

| Package | Description |
| ------- | ----------- |
| [`wan24-RPC`](https://www.nuget.org/packages/wan24-RPC/) | Core RPC functionality |
| [`wan24-RPC-Service`](https://www.nuget.org/packages/wan24-RPC-Service/) | RPC service functionality |

## Usage

### Creating a RPC API

A RPC API is any type which exports public methods. You should use the 
`(Disposable)RpcApiBase` base type, 'cause it uses the `NoRpcAttribute` on 
methods which should not be remote callable. A short overview over the helpers 
that you may want to use:

| Helper | Description |
| ------ | ----------- |
| `RpcApiBase` | RPC API base type |
| `DisposableRpcApiBase` | Disposable RPC API base type |
| `RpcAliasAttribute` | Attribute for APIs or methods which should be exported using a different name |
| `NoRpcAttribute` | Attribute for public methods or parameters which shouldn't be accessed from the peer |
| `NoRpcDisposeAttribute` | Attribute for RPC methods which return a disposable value which should NOT be disposed after sending it to the peer, or for API classes which should NOT be disposed, if disconnected |
| `NoRpcEnumerableAttribute` | Attribute for RPC method return value or parameters which are enumerables, but shouldn't be handled as enumerables during RPC call processing |
| `NoRpcCompressionAttribute` | Attribute for RPC method stream return value or parameters which shouldn't use compression ('cause they may be compressed already) |

Per default the API class and method names are used for addressing a RPC call. 
However, it's possible to add API classes and methods using customized names.

If you use the pre-defined RPC service logic, `wan24.Core.DiHelper` is being 
used for dependency injection, so you may use keyed services also.

### RPC stream extensions

Any blocking bi-directional stream can be used as RPC communication stream:

```cs
// Reading any RPC message
RpcMessageBase message = await stream.ReadRpcMessageAsync();

// Writing any RPC message
await stream.WriteAnyRpcMessageAsync(message);
```

`RpcMessageBase` is just a base type, which is being used by 

- `RpcRequestBase` which is a remote RPC call
- `RpcResponseBase` which is a remote RPC call response

Those base types are used by

- `SerializedRpc(Request/Response)Message` which use 
[`Stream-Serializer-Extensions`](https://github.com/nd1012/Stream-Serializer-Extensions) 
for binary serialization
- `JsonRpc(Request/Response)Message` which use `wan24.Core.JsonHelper` for 
JSON serialization
- `RpcErrorResponseMessage`
- `RpcEventMessage` (which is a request which doesn't want a response)

and must be used as base type for your own implementations. Each RPC message 
type has an ID, which may be sent to the peer before the serialized message 
body. Your custom RPC message type needs to be registered:

```cs
RpcMessageTypes.Register<YourRpcMessage>(1 << 8, yourSerializer, yourDeserializer);
```

**NOTE**: The first 8 bit of the message type ID are reserved, so your custom 
message type ID must start from `256+`.

### Using the RPC processor

The RPC processor is used to evaluate a RPC call to registered RPC API classes 
and methods:

```cs
RpcProcessor processor = new(new(){ ApiTypes = [typeof(YourRpcApi)] });
await using(processor)
{
	await processor.StartAsync();
	while(peer.IsConnected)
	{
		// Receive a request message, first
		...
		// Then evaluate it
		processor.Evaluate(message, yourReturnValueHandler);
		// The return value will be managed from the RPC processor automatic
	}
}
```

**NOTE**: One RPC processor instance is required for each RPC peer connection!

**WARNING**: The number of processing evaluations is limited (using the RPC 
processor options). Any limit exceeding call will cause an exception at the 
peer!

### Using the RPC service

You may use the `RpcServiceBase` as base class for your RPC service. The 
`RpcServiceBase` is a hosted service, which you can use with an app host:

```cs
builder.Services.AddHostedService<YourRpcService>();
```

Example RPC service implementation:

```cs
public class YourRpcService : RpcServiceBase
{
	protected override async Task WaitForPeersAsync()
	{
		while(true)
		{
			// Wait for a connecting peer
			AddProcessorConnection(new WrapperStream(rpcStream), rpcProcessor);
		}
	}
}
```

**NOTE**: `RpcServiceBase` is included in the `wan24-RPC-Service` NuGet 
package.

**NOTE**: `rpcStream` must be an `IStream` which is being disposed when the 
connection was closed! You can use `WrapperStream` to adopt any stream to the 
`IStream` interface without any hussle.

**NOTE**: Exceptions thrown during evaluation will cause an error response, 
which will cause a `RpcException` at the waiting peer.

Many methods can be overridden to customize the RPC service for your needs.

### Using the RPC SDK

The `RpcSdkBase` is a base class for creating a SDK which uses a RPC stream:

```cs
public class YourSdk : RpcSdkBase
{
	public YourSdk() : base() { }

	public async Task ConnectAsync()
	{
		// Connect to the RPC server
		...
		// Then set the RPC stream (must be an IStream!)
		RPC = new WrapperStream(rpcStream);
	}

	public Task<AnyType> YourApiMethodAsync(AnyType2 parameter, CancellationToken ct = default)
		=> SendRpcCallAsync<AnyType>(nameof(YourApiMethodAsync), ct, parameter);
}
```

**NOTE**: `rpcStream` must be a `IStream` which is being disposed when the 
connection was closed! You can use `WrapperStream` to adopt any stream to the 
`IStream` interface without any hussle.

The SDK will manage a RPC processor, if clientside RPC calls are enabled.

API usage:

```cs
YourSdk sdk = new();
await using(sdk)
{
	// Configure the SDK connection
	...
	// Then connect
	await sdk.ConnectAsync();
	// Then call API methods
	AnyType result;
	try
	{
		result = await sdk.YourApiMethodAsync(value);
	}
	catch(RpcException ex)
	{
		// Handle the remote execution error
		// (InnerException has the remote exception type, if possible)
	}
	...
}
```

**TIP**: For keeping consistence between the client and server RPC API and SDK 
implementations you can use a shared interface and implement an API method for 
pre-checking the API version, and if a client update is required, first.

### Enumerable parameters and return values

Enumerables will be transfered asynchronous (streamed) and managed by the RPC 
processor automatic, if the type at the receiving side implements `IList`. A 
parameter or return value may be an `IEnumerable<T>` or `IAsyncEnumerable<T>`.

**NOTE**: To disable that behavior you can use the `NoRpcEnumerableAttribute` 
for your RPC API or SDK method or RPC API parameter. For SDK method parameters 
wrap the value with `NoRpcEnumerable`.

**TIP**: Use `IAsyncEnumerable<T>` where possible!

**WARNING**: The number of processing enumerables is limited (using the RPC 
processor options). Any limit exceeding call will cause an exception at the 
peer!

### Stream parameters and return values

Streams will be transfered asynchronous and managed by the RPC processor 
automatic. While synchronous stream usage is possible, all synchronous stream 
methods are mapped to asynchronous methods (you should avoid using them!).

**NOTE**: If compression is enabled, streams will be transfered compressed per 
default. If you don't want that, you can use the `NoRpcCompressionAttribute` 
for your RPC API or SDK method or RPC API parameter. For SDK method parameters 
wrap a value with `UncompressedRpcStream`.

**WARNING**: The number of processing streams is limited (using the RPC 
processor options). Any limit exceeding call will cause an exception at the 
peer!

### Events

A `RpcProcessor` and a `RpcSdkBase` offer a simple solution for events using 
`RpcEventMessage` and `RpcEvent`.

In a processor or SDK you can register events like this:

```cs
RegisterEvent(new(nameof(YourEvent), typeof(YourEventData), RaiseYourEvent));
```

Example event definition:

```cs
public delegate void YourEvent_Delegate(object sender, YourEventData e);
public event YourEvent_Delegate? YourEvent;
private async void RaiseYourEvent(RpcEvent rpcEvent, object? e)
{
	await Task.Yield();
	YourEvent?.Invoke(this, (YourEventData)e!);
}
```

**NOTE**: Since event handling is a synchronous operation, it's not possible 
to write something back to the event data and respond it to the RPC peer.

To send any event to the peer:

```cs
anyObject.AnyEvent += (sender, e)
	=>  SendEvent(nameof(AnyObjectType.AnyEvent), new AnyEventData(sender, e));
```

**NOTE**: The `SendEvent` method doesn't return a task, but will send the 
event asynchronous in background. It's part of the processor and the SDK base 
typed and may be called in any other context, also.

**WARNING**: You should remove event listeners in order to enable the GC to 
clan up the object reference, if a processor or a SDK instance isn't in use 
anymore!

The event message processing will be handled by the RPC processor automatic.
