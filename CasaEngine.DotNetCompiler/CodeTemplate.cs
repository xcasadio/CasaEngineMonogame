using System.Reflection;

namespace CasaEngine.DotNetCompiler;

public abstract class CodeTemplate
{
    protected string _generatedCode;
    public virtual string GeneratedCodeNamespaceName => "MyNamespace";
    public virtual string GeneratedCodeClassName => "MyClass";
    public virtual string GeneratedCodeMethodName => "Run";

    public abstract CodeTemplateResult CreateInstance(DotNetCallArguments instanceArgs, Assembly assembly);

    public string Build(DotNetDynamicScriptParameter scriptParameters)
    {
        var _generatedCode = GetCodeTemplate();
        scriptParameters.Imports.AddRange(DefaultImports);
        BuildImports(scriptParameters.Imports, ref _generatedCode);
        BuildMethodParameters(scriptParameters, ref _generatedCode);
        BuildBody(scriptParameters.Script, ref _generatedCode);
        return _generatedCode;
    }

    public virtual string GetInstanceSignature() => $"{GeneratedCodeNamespaceName}.{GeneratedCodeClassName}";
    public virtual string GetMethodName() => GeneratedCodeMethodName;

    protected abstract List<string> DefaultImports { get; }

    public abstract int GetCodeLineOffset(string code);

    protected abstract string GetCodeTemplate();

    protected abstract void BuildMethodParameters(DotNetDynamicScriptParameter p, ref string code);

    protected abstract void BuildImports(List<string> imports, ref string code);

    private void BuildBody(string methodBody, ref string code)
    {
        code = code.Replace("{code}", methodBody);
    }
}