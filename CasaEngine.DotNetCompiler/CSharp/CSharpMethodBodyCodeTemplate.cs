namespace CasaEngine.DotNetCompiler.CSharp;

public class CSharpMethodBodyCodeTemplate : MethodBodyCodeTemplate
{
    protected override string GetCodeTemplate()
        => @$"{{usings}}

namespace {GeneratedCodeNamespaceName} {{
    public class {GeneratedCodeClassName} {{
        public void {GeneratedCodeMethodName}({{methodParameters}}) {{
            {{code}}
        }}
    }}
}}";

    public override int GetCodeLineOffset(string code) =>
        code.Substring(0, code.IndexOf($"public void {GeneratedCodeMethodName}"))
            .Count(x => x == '\n');

    protected override List<string> DefaultImports => new()
    {
        "System",
        "System.Linq"
    };

    protected override string DoBuildMethodParameters(List<ParameterDefinition> parameterDefinitions)
    {
        var methodParams = string.Join(", ", parameterDefinitions.Select(x => $"{(x.Direction == ParameterDirection.Output || x.Direction == ParameterDirection.InputOutput ? "ref " : string.Empty)}{MapToCSharpType(x.Type)} {x.Key}"));
        return methodParams;
    }

    protected override void BuildImports(List<string> imports, ref string code)
    {
        code = code.Replace("{usings}", string.Join(Environment.NewLine, imports.Select(x => $"using {x};")));
    }

    private string MapToCSharpType(ParameterDefinitionType type)
    {
        return type switch
        {
            ParameterDefinitionType.Bool => "bool",
            ParameterDefinitionType.Int => "int",
            ParameterDefinitionType.Datetime => "DateTime",
            ParameterDefinitionType.Datatable => "DataTable",
            ParameterDefinitionType.Double => "double",
            ParameterDefinitionType.Decimal => "decimal",
            ParameterDefinitionType.Dynamic => "dynamic",
            ParameterDefinitionType.Long => "long",
            ParameterDefinitionType.String => "string",
            ParameterDefinitionType.List => "dynamic",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}