using System.Reflection;

namespace CasaEngine.DotNetCompiler;

public class ClassCodeTemplate : CodeTemplate
{
    public override int GetCodeLineOffset(string code) => 0;

    protected override string GetCodeTemplate() => "{code}";

    protected override List<string> DefaultImports => new List<string>();

    protected override void BuildImports(List<string> imports, ref string code) { }

    protected override void BuildMethodParameters(DotNetDynamicScriptParameter p, ref string code) { }

    public override CodeTemplateResult CreateInstance(DotNetCallArguments args, Assembly assembly)
    {
        // foreach class find method? i to methodtocall na einai klasi me ola ta parameters
        object instance = null;
        if (string.IsNullOrEmpty(args.InstanceSignature))
        {
            var type = assembly.GetExportedTypes().FirstOrDefault(x => x.GetMethod(args.MethodName) != null);
            instance = Activator.CreateInstance(type);
        }
        else
            instance = assembly.CreateInstance(args.InstanceSignature);

        var method = instance.GetType().GetMethod(args.MethodName ?? GetMethodName());

        return new CodeTemplateResult(instance, method);
    }
}