#if !RELEASE //TODO Enable when fully implemented
namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for a RPC API type which should be included for SDK generation
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateSdkAttribute() : Attribute()
    {
        /// <summary>
        /// SDK type name (f.e. <c>YourSdk</c>) which includes the API
        /// </summary>
        public string? Sdk { get; set; }
    }
}
#endif
