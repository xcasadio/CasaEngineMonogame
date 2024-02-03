using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CasaEngine.DotNetCompiler.CSharp;

public class CSharpDynamicScriptController : DotNetDynamicScriptController
{
    public CSharpDynamicScriptController(CodeTemplate codeTemplate) : base(codeTemplate)
    {
    }

    protected override Compilation GetCompilationForAssembly(string assemblyName) => CSharpCompilation.Create(assemblyName);

    protected override CompilationOptions GetOptions() => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

    protected override SyntaxTree GetSyntaxTree(string code)
    {
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp11);
        return CSharpSyntaxTree.ParseText(code);
    }

    protected override Compilation AddReferences(string rootPath, Compilation compilation)
    {
        return compilation.AddReferences(MetadataReference.CreateFromFile(Path.Combine(rootPath, "Microsoft.CSharp.dll")));
    }
}