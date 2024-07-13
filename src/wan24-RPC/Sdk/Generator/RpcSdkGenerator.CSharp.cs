using System.Text;
using System.Web;

namespace wan24.RPC.Sdk.Generator
{
    // C#
    public static partial class RpcSdkGenerator
    {
        /// <summary>
        /// Generate a C# SDK for an API (won't create DTOs!)
        /// </summary>
        /// <param name="options">Options</param>
        /// <returns>C# SDK source code</returns>
        public static string GenerateCSharp(in RpcCSharpSdkGeneratorOptions options)
        {
            StringBuilder res = new();
            res.AppendLine("using System.Threading.Tasks;");
            res.AppendLine("using wan24.Core;");
            res.AppendLine("using wan24.RPC.Processing;");
            res.AppendLine("using wan24.RPC.Sdk;");
            res.AppendLine();
            res.AppendLine($"namespace {options.NameSpace};");
            res.AppendLine();
            res.AppendLine("/// <summary>");
            res.AppendLine($"/// {string.Join($"\r\n/// ", options.Description.Replace("\r", string.Empty).Split('\n').Select(d => HttpUtility.HtmlEncode(d)))}");
            res.AppendLine("/// </summary>");
            res.AppendLine($"public partial class {options.Sdk} : {(options.Extended ? $"RpcSdkBaseExt<{options.RpcProcessor}>" : $"RpcSdkBase<{options.RpcProcessor}>")}");
            res.AppendLine("{");
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// Running SDK initialization completion");
            res.AppendLine("\t/// </summary>");
            res.AppendLine("\tprotected TaskCompletionSource? Initializing = null;");
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// If the SDK was initialized by calling <see cref=\"InitializeAsync(CancellationToken)\"/>");
            res.AppendLine("\t/// </summary>");
            res.AppendLine("\tprotected bool Initialized = false;");
            // Constructors
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// Constructor");
            res.AppendLine("\t/// </summary>");
            res.AppendLine($"\tprotected {options.Sdk}() : base()");
            res.AppendLine("\t{");
            if (!options.DisposeProcessor)
                res.AppendLine("\t\tDisposeProcessor = false;");
            res.AppendLine("\t}");
            res.AppendLine();
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// Constructor");
            res.AppendLine("\t/// </summary>");
            res.AppendLine($"\t/// <param name=\"processor\">RPC processor {(options.DisposeProcessor ? "(will be disposed!)" : "(won't be disposed)")}</param>");
            res.AppendLine($"\tpublic {options.Sdk}(in {options.RpcProcessor} processor) : base(processor)");
            res.AppendLine("\t{");
            if (!options.DisposeProcessor)
                res.AppendLine("\t\tDisposeProcessor = false;");
            res.AppendLine("\t}");
            // Asynchronous initialization
            res.AppendLine();
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// Initialize the SDK properly before usage");
            res.AppendLine("\t/// </summary>");
            res.AppendLine("\t/// <param name=\"cancellationToken\">Cancellation token</param>");
            res.AppendLine("\t/// <returns>If this call initialized the SDK, finally</returns>");
            res.AppendLine("\tpublic virtual async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)");
            res.AppendLine("\t{");
            res.AppendLine("\t\t// Mandatory pre-usage checks");
            res.AppendLine("\t\tEnsureUndisposed();");
            res.AppendLine("\t\tEnsureInitialized();");
            res.AppendLine("\t\t// Synchronize the initialization process");
            res.AppendLine("\t\tbool initialize;");
            res.AppendLine("\t\tTaskCompletionSource? tcs = Initializing;");
            res.AppendLine("\t\tlock (Cancellation)");
            res.AppendLine("\t\t{");
            res.AppendLine("\t\t\tinitialize = !Initialized && tcs is null;");
            res.AppendLine("\t\t\tif (initialize)");
            res.AppendLine("\t\t\t\ttcs = Initializing = new(TaskCreationOptions.RunContinuationsAsynchronously);");
            res.AppendLine("\t\t}");
            res.AppendLine("\t\t// Wait for another initialization task to complete");
            res.AppendLine("\t\tif (!initialize)");
            res.AppendLine("\t\t{");
            res.AppendLine("\t\t\tif (tcs is not null)");
            res.AppendLine("\t\t\t\tawait tcs.Task.WaitAsync(cancellationToken).DynamicContext();");
            res.AppendLine("\t\t\treturn false;");
            res.AppendLine("\t\t}");
            res.AppendLine("\t\t// Process the initialization");
            res.AppendLine("\t\ttry");
            res.AppendLine("\t\t{");
            res.AppendLine("\t\t\tawait InitializeIntAsync(cancellationToken).DynamicContext();");
            res.AppendLine("\t\t\tInitialized = true;");
            res.AppendLine("\t\t\treturn true;");
            res.AppendLine("\t\t}");
            res.AppendLine("\t\tcatch (Exception ex)");
            res.AppendLine("\t\t{");
            res.AppendLine("\t\t\t// Let waiting initialization tasks fail, too");
            res.AppendLine("\t\t\ttcs.TrySetException(ex);");
            res.AppendLine("\t\t\tthrow;");
            res.AppendLine("\t\t}");
            res.AppendLine("\t\tfinally");
            res.AppendLine("\t\t{");
            res.AppendLine("\t\t\tInitializing = null;");
            res.AppendLine("\t\t\ttcs.TrySetResult();");
            res.AppendLine("\t\t}");
            res.AppendLine("\t}");
            res.AppendLine();
            res.AppendLine("\t/// <summary>");
            res.AppendLine("\t/// Initialize the SDK properly before usage (called ONCE by <see cref=\"InitializeAsync(CancellationToken)\"/>)");
            res.AppendLine("\t/// </summary>");
            res.AppendLine("\t/// <param name=\"cancellationToken\">Cancellation token</param>");
            res.AppendLine("\tprotected virtual Task InitializeIntAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;");
            // Methods
            //TODO Generate SDK methods code
            res.AppendLine("}");
            return res.ToString();
        }
    }
}
