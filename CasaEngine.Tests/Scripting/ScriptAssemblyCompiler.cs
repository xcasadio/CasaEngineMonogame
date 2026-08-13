using System.Runtime.InteropServices;
using CasaEngine.Framework.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// Compiles small gameplay script assemblies on the fly for load/unload tests.
/// </summary>
internal static class ScriptAssemblyCompiler
{
    public static string CompileScriptAssembly(string directory, string assemblyName, int version)
    {
        string source = $$"""
            using CasaEngine.Framework.Physics;
            using CasaEngine.Framework.Scripting;

            namespace TestScripts;

            public class TestScriptProxy : GameplayProxy
            {
                public static int Version => {{version}};

                protected override void InitializePrivate() { }
                public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world) { }
                public override void Update(float elapsedTime) { }
                public override void Draw() { }
                public override void OnHit(Collision collision) { }
                public override void OnHitEnded(Collision collision) { }
                public override void OnBeginPlay(CasaEngine.Framework.Scene.World.World world) { }
                public override void OnEndPlay(CasaEngine.Framework.Scene.World.World world) { }
                public override IGameplayProxy Clone() => new TestScriptProxy();
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "netstandard.dll")),
            MetadataReference.CreateFromFile(typeof(GameplayProxy).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Directory.CreateDirectory(directory);
        string assemblyPath = Path.Combine(directory, assemblyName + ".dll");
        var emitResult = compilation.Emit(assemblyPath);

        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine,
                emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Test script compilation failed:{Environment.NewLine}{errors}");
        }

        return assemblyPath;
    }
}
