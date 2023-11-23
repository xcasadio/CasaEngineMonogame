using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

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

#if EDITOR

    public FlowGraphManager FlowGraph { get; } = new();

    public string CompiledCodeFileName { get; set; }

    public ExternalComponent InstanciatedObject { get; set; }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //FlowGraph.Save(jObject, option);
    }
#endif
}