using System.Data;
using System.Reflection;
using CasaEngine.Compiler;
using CasaEngine.Compiler.Errors;
using CasaEngine.Compiler.Results;
using Microsoft.CodeAnalysis;

namespace CasaEngine.DotNetCompiler;

public abstract class DotNetDynamicScriptController : IDynamicScriptController<DotNetDynamicScriptParameter, DotNetCallArguments>
{
    private Assembly _assembly;
    private DotNetDynamicScriptParameter _operationParams;

    private readonly CodeTemplate _codeTemplate;

    protected DotNetDynamicScriptController(CodeTemplate codeTemplate)
    {
        _codeTemplate = codeTemplate;
    }

    public IEnumerable<ParameterDefinition> OutputParameters
        => _operationParams.Parameters.Where(x => x.Direction is ParameterDirection.Output or ParameterDirection.InputOutput);

    public object CreateInstance(string namespaceName, string className)
    {
        var dotNetCallArguments = new DotNetCallArguments(namespaceName, className, null);
        return _codeTemplate.CreateInstance(dotNetCallArguments, _assembly).Instance;
    }

    public EvaluationResult Evaluate(DotNetDynamicScriptParameter parameter)
    {
        _operationParams = parameter;

        var code = _codeTemplate.Build(parameter);

        var syntaxTree = GetSyntaxTree(code);

        var rootPath = Path.GetDirectoryName(typeof(object).Assembly.Location) + Path.DirectorySeparatorChar;

        var compilation = GetCompilationForAssembly(parameter.AssemblyName)
            .WithOptions(GetOptions())
            .AddSyntaxTrees(syntaxTree);

        compilation = AddDefaultReferences(rootPath, compilation);
        compilation = AddReferences(rootPath, compilation);

        foreach (var reference in parameter.References)
        {
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(reference));
        }

        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error);
        if (errors.Any())
        {
            int lineOffset = _codeTemplate.GetCodeLineOffset(code);
            return EvaluationResult.WithErrors(errors.Select(x => GetDynamicScriptError(x, lineOffset)));
        }

        using (var ms = new MemoryStream())
        {
            string resourcesFileLocation = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "resources.resx");

            var result = compilation.Emit(ms, pdbStream: null);

            if (!result.Success)
            {
                int lineOffset = _codeTemplate.GetCodeLineOffset(code);
                return EvaluationResult.WithErrors(result.Diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => GetDynamicScriptError(x, lineOffset)));
            }
            var assemblyBytes = ms.ToArray();
            _assembly = Assembly.Load(assemblyBytes);
        }

        return EvaluationResult.Ok();
    }

    private DynamicScriptCompilationError GetDynamicScriptError(Diagnostic d, int lineOffset)
    {
        var error = new DynamicScriptCompilationError(d.GetMessage(),
            d.Location.GetLineSpan().StartLinePosition.Line - lineOffset,
            d.Location.GetLineSpan().EndLinePosition.Line - lineOffset,
            d.Location.SourceSpan.Start,
            d.Location.SourceSpan.Length);
        return error;
    }

    public ExecutionResult Execute(DotNetCallArguments callArgs = null, List<ParameterArgument> methodArgs = null)
    {
        try
        {
            var instanceResult = _codeTemplate.CreateInstance(callArgs ?? new DotNetCallArguments(), _assembly);

            var arguments = ParseMethodArguments(methodArgs, instanceResult.Method);
            var methodResult = instanceResult.Method.Invoke(instanceResult.Instance, arguments);

            var res = ExecutionResult.Ok();
            if (instanceResult.Method.ReturnType != typeof(void))
            {
                res.Add(new MethodReturnValue(methodResult));
            }

            foreach (var outParam in OutputParameters)
            {
                var entry = methodArgs.FirstOrDefault(x => x.Key == outParam.Key);
                var index = _operationParams.Parameters.IndexOf(outParam);

                res.Add(new ExecutionResultEntry(outParam.Key, arguments[index]));
            }

            return res;
        }
        catch (TargetInvocationException tie)
        {
            return ExecutionResult.WithError(tie);
        }
        catch (ArgumentException ae)
        {
            return ExecutionResult.WithError(ae);
        }
    }

    private object[] ParseMethodArguments(List<ParameterArgument> methodArgs, MethodInfo method)
    {
        List<object> arguments = new List<object>();
        foreach (var parameter in method.GetParameters())
        {
            var arg = methodArgs.FirstOrDefault(x => x.Key == parameter.Name);
            arguments.Add(arg?.Value);
        }
        return arguments.ToArray();
    }

    protected abstract Compilation GetCompilationForAssembly(string assemblyName);

    protected abstract CompilationOptions GetOptions();

    protected abstract SyntaxTree GetSyntaxTree(string code);

    private Compilation AddDefaultReferences(string rootPath, Compilation compilation)
    {
        return compilation.AddReferences(MetadataReference.CreateFromFile(typeof(int).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(DataTable).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(File).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(AssemblyTitleAttribute).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(System.Text.StringBuilder).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(Path.Combine(rootPath, "System.dll")))
            .AddReferences(MetadataReference.CreateFromFile(Path.Combine(rootPath, "netstandard.dll")))
            .AddReferences(MetadataReference.CreateFromFile(Path.Combine(rootPath, "System.Runtime.dll")));
    }

    protected virtual Compilation AddReferences(string rootPath, Compilation compilation)
        => compilation;
}