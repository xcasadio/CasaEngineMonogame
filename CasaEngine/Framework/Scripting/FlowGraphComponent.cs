using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using FlowGraph;

namespace CasaEngine.Framework.Scripting;

public class FlowGraphComponent : ExternalComponent
{

    public override int ExternalComponentId { get; }

    public override void Initialize(Entity entity)
    {
    }

    public override void Update(float elapsedTime)
    {
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World.World world)
    {
    }

    public override void OnEndPlay(World.World world)
    {

    }

    public override void Load(JsonElement element, SaveOption option)
    {
    }

#if EDITOR

    public FlowGraphManager FlowGraph { get; } = new();

    public string CompiledCodeFileName { get; set; }

    public ExternalComponent InstanciatedObject { get; set; }
}

#endif

    /*public bool CompiledFlowGraph()
    {
        var memoryStream = new MemoryStream();
        var dotNetWriter = new DotNetWriter(FlowGraph);
        dotNetWriter.Write(memoryStream);

        var controller = new CSharpDynamicScriptController(new ClassCodeTemplate());
        var result = controller.Evaluate(new DotNetDynamicScriptParameter(@"using System;
namespace Test
{
    public class TestClass
    {
        public int Run() {
            return 1;
        } 
    }
}"));

        var executionResult = controller.Execute(
            new DotNetCallArguments(namespaceName: "Test", className: "TestClass", methodName: "Run"),
            new List<ParameterArgument>() { });

        return true;
    }*/
}