using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24_RPC_Tests
{
    [Rpc]
    public sealed class DisposableTestObject() : DisposableStreamSerializerBase()
    {
        protected override void Serialize(Stream stream) { }

        protected override void Deserialize(Stream stream, int version) { }

        protected override void Dispose(bool disposing) { }
    }
}
