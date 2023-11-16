using System.Reflection;

namespace CasaEngine.DotNetCompiler;

public class CodeTemplateResult
{
    public object Instance { get; }
    public MethodInfo Method { get; }

    public CodeTemplateResult(object instance, MethodInfo method)
    {
        Instance = instance;
        Method = method;
    }
}