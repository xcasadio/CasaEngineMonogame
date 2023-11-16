using System.Reflection;

namespace CasaEngine.DotNetCompiler;

public abstract class MethodBodyCodeTemplate : CodeTemplate
{
    protected override void BuildMethodParameters(DotNetDynamicScriptParameter operationParams, ref string code)
    {
        var methodParameters = string.Empty;
        if (operationParams != null && operationParams.Parameters != null && operationParams.Parameters.Any())
            methodParameters = DoBuildMethodParameters(operationParams.Parameters);
        code = code.Replace("{methodParameters}", methodParameters);
    }

    protected abstract string DoBuildMethodParameters(List<ParameterDefinition> parameterDefinitions);

    public override CodeTemplateResult CreateInstance(DotNetCallArguments instanceArgs, Assembly assembly)
    {
        var instance = assembly.CreateInstance(GetInstanceSignature());
        var method = instance.GetType().GetMethod(GetMethodName());

        return new CodeTemplateResult(instance, method);
    }
}