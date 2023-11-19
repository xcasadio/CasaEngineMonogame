using System.Text.Json;
using CasaEngine.Compiler;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;
using System.IO;
using CasaEngine.DotNetCompiler;
using CasaEngine.DotNetCompiler.CSharp;
using System.Diagnostics;

#if EDITOR
using FlowGraph;
#endif

namespace CasaEngine.Framework.Scripting;

public class EntityFlowGraph : Entity
{
    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        //FlowGraph.Load(element.GetElement("flow_graph"), option);
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //FlowGraph.Save(jObject, option);
    }

#if EDITOR

    public FlowGraphManager FlowGraph { get; } = new();

    public string CompiledCodeFileName { get; set; }

    public ExternalComponent InstanciatedObject { get; set; }

    public bool CompileFlowGraph()
    {
        var stream = new StringWriter();
        var dotNetWriter = new CSharpWriter(stream);
        dotNetWriter.GenerateCode(FlowGraph);

        var controller = new CSharpDynamicScriptController(new ClassCodeTemplate());
        var result = controller.Evaluate(new DotNetDynamicScriptParameter(stream.ToString()));

        /*var executionResult = controller.Execute(
            new DotNetCallArguments(namespaceName: "Test", className: "TestClass", methodName: "Run"),
            new List<ParameterArgument>() { });*/

        return result.Success;
    }

#endif
}