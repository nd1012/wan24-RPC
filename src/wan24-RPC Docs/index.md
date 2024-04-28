# wan24-RPC

This library contains some RPC helper which enables an app to use any 
bi-directional RPC stream on the fly. It supports 

- Stream channels
- Enumeration channels
- Events
- Cancellation
- API versioning
- Compression
- Server AND client RPC (bi-directional)
- Authorization

I try to keep the API of this library as abstract as possible, but giving a 
fully working environment. I've decided to concentrate on streams, which seem 
to be the best center for an abstraction logic.

While the RPC stream is the central element, all other things (processor, SDK) 
are optional and just suggestions that are built on each other to let you make 
the decision how far you want or need to go, finally, and to offer everything 
to go for rapid app development.

**NOTE**: This library requires a .NET server AND client. No other languages 
are supported at present.

**NOTE**: The server side implementation requires components which are not 
part of this library.

## How to get it

This library is available as 
[NuGet package](https://www.nuget.org/packages/wan24-RPC/).

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
| `RpcAuthorizationAttributeBase` | Base type for an authorization attribute for APIs or methods |
| `RpcAuthorizedAttribute` | Attribute for single API methods which don't require an authorization (if the API itself requires it) |
| `RpcAliasAttribute` | Attribute for APIs or methods which should be exported using a different name |
| `NoRpcAttribute` | Attribute for public methods or parameters which shouldn't be accessed from the peer |
| `NoRpcDisposeAttribute` | Attribute for RPC methods which return a disposable value which should NOT be disposed after sending it to the peer, or for API classes which should NOT be disposed, if disconnected |
| `NoRpcEnumerableAttribute` | Attribute for RPC method return value or parameters which are enumerables, but shouldn't be handled as enumerables during RPC call processing |
| `NoRpcCompressionAttribute` | Attribute for RPC method stream return value or parameters which shouldn't use compression ('cause they may be compressed already) |
| `RpcVersionAttribute` | Attribute for API or SDK methods which restrict the supported peer API version (evaluated by the RPC processor) |

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
await stream.WriteRpcMessageAsync(message);
```

`RpcMessageBase` is just a base type, which is being used by 

- `RpcRequestBase`
- `RpcResponseBase`
- `RpcEventMessageBase`
- `RpcErrorResponseMessage`
- `RpcCancellationMessage`

Those base types are used by

- `SerializedRpc*(Request/Response)Message` which use 
[`Stream-Serializer-Extensions`](https://github.com/nd1012/Stream-Serializer-Extensions) 
for binary serialization
- `JsonRpc*(Request/Response)Message` which use `wan24.Core.JsonHelper` for 
JSON serialization

and must be used as base type for your own implementations. Each RPC message 
type has an ID, which may be sent to the peer before the serialized message 
body. Your custom RPC message type needs to be registered:

```cs
RpcMessageTypes.Register<YourRpcMessage>(1 << 8);
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
		// Then set the RPC stream
		RPC = rpcStream;
	}

	public Task<AnyType> YourApiMethodAsync(AnyType2 parameter, CancellationToken ct = default)
		=> SendRpcCallAsync<AnyType>(nameof(YourApiMethodAsync), ct, parameter);
}
```

The SDK will manage a RPC processor, if clientside RPC calls are enabled.

SDK usage:

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
`*RpcEventMessage` and `RpcEvent`.

In a processor or SDK you can register receivable events like this:

```cs
RegisterEvent(new(nameof(YourEvent), typeof(YourEventData), RaiseYourEvent));
```

Example receivable event definition:

```cs
public delegate void YourEvent_Delegate(object sender, YourEventData e);
public event YourEvent_Delegate? YourEvent;
private async void RaiseYourEvent(RpcEvent rpcEvent, object? e)
{
	await Task.Yield();
	if(e is not YourEventData data)
		// Exception will be handled by the processor or SDK
		throw new InvalidProgramException("Missing/invalid event data");
	YourEvent?.Invoke(this, data);
}
```

**NOTE**: Since event handling is a synchronous operation, it's not possible 
to write something back to the event data and respond it to the RPC peers 
event sending context.

To send any event to the peer:

```cs
anyObject.AnyEvent += (sender, e)
	=>  SendEvent(nameof(AnyObjectType.AnyEvent), new AnyEventData(sender, e));
```

The event message processing will be handled by the RPC processor automatic.

**NOTE**: The `SendEvent` method doesn't return a task, but will send the 
event asynchronous in background. It's part of the processor and the SDK base 
types and may be called in any other context, also.

**TIP**: You can enable event throttling by defining an event throttler 
instance in the RPC processor options.

**WARNING**: You should remove event listeners in order to enable the GC to 
clan up the object reference, if a processor or a SDK instance isn't in use 
anymore!

### Cancellation

If a SDK method timed out, the SDK will send a `RpcCancellationMessage` to the 
peer, which will then cancel the RPC method execution, if possible. For this 
the RPC API method needs to have a `CancellationToken` type parameter.

### API versioning

You need to implement a version check into your SDK by yourself. It's 
required to use an unsigned integer number as version number. Using the 
`RpcVersionAttribute` you can restrict the supported API version and also 
forward a RPC processor to another API method for the used SDK version:

```cs
public async Task<int> ApiVersionAsync(RpcProcessor processor, int peerSdkVersion, CancellationToken ct)
{
	// Check the version number
	...
	// Then use it
	processor.CurrentApiVersion = peerSdkVersion;
	// Maybe return the latest API version to signal an available optional update
	return YourApi.VERSION;
}

[RpcVersion(fromVersion: 1, toVersion: 3, newerMethodName: "YourApiMethodV2Async")]
public async Task YourApiMethodAsync(AnyType parameter, CancellationToken ct)
{
	// Used with peer versions 1, 2 and 3
}

[RpcVersion(4)]
public async Task<ReturnType> YourApiMethodV2Async(AnyType parameter, OtherType other, CancellationToken ct)
{
	// Used from peer versions 4+
}
```

To enable API version restrictions your API needs to set the 
`CurrentApiVersion` value to the RPC processor which manages the connection. 
Any RPC call to an API method which has a `RpcVersionAttribute` before the 
`CurrentApiVersion` value was set will cause an error at the client side.

Since communicated objects are being serialized, and they may be changed, too, 
you could use the serializer versioning for `IStreamSerializer` 
implementations (see the documentation of the `Stream-Serializer-Extensions` 
NuGet package). The serializer versioning is fully independent from the API 
versioning. Anyway, if the serializer version of a communicated type 
increases, you should also increase the API version number to be safe, if the 
updated serialization requires a peer software update (which is the case when 
the new serialization doesn't support the previous object structure).

A serializer version increment is required, if the new type revision is 
incompatible with the previous type revision.

An API version increment is required on any incompatibility:

- Incompatible types
- Method signature changes
- Method removals

For any other change incrementing a version number is optional and depends on 
the context of the change, and if the change is downward compatible.

### Authorization

Use the `RpcAuthorizationAttributeBase` type for implementing authorization 
for your RPC APIs and methods:

```cs
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class YourAuthorizationAttribute : RpcAuthorizationAttributeBase
{
	public override async Task<bool> IsAuthorizedAsync(RpcContext context)
	{
		// Determine if the context is authorized and return TRUE to continue, 
		// or FALSE to disconnect the peer
	}
}
```

This attribute can be applied to RPC API classes and methods and will be 
evaluated by the RPC processor before executing any API method. Any 
unauthorized access will disconnect the peer:

```cs
[YourAuthorization]// Optional for all API methods
public class YourRpcApi : RpcApiBase
{
	[YourAuthorization]// Optional at the API method level
	public async Task YourApiMethod()
	{
		...
	}
	...
}
```

Using the `RpcAuthorizedAttribute` you can disable authorization for single 
methods of an API which requires authorization for all exported API methods.

**CAUTION**: If you mix `RpcAuthorizedAttribute` with any 
`RpcAuthorizationAttributeBase` attributes, no authorization will be required, 
finally!
