using CasaEngine.Compiler;

namespace CasaEngine.DotNetCompiler;

public class DotNetCallArguments : CallArguments
{
    public string NamespaceName { get; }
    public string ClassName { get; }

    public DotNetCallArguments(string namespaceName = null, string className = null, string methodName = null)
        : base(methodName ?? "Main")
    {
        NamespaceName = namespaceName;
        ClassName = className;
    }

    public string InstanceSignature
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(NamespaceName) && !string.IsNullOrWhiteSpace(ClassName))
            {
                return $"{NamespaceName}.{ClassName}";
            }

            if (string.IsNullOrWhiteSpace(NamespaceName) && !string.IsNullOrWhiteSpace(ClassName))
            {
                return ClassName;
            }

            return string.Empty;
        }
    }
}