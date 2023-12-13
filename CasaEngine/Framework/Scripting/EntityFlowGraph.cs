using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

#if EDITOR
using CasaEngine.Framework.Entities.Components;
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

    public ExternalComponent InstanciatedObject { get; private set; }

    public void InitializeScript(ExternalComponent externalComponent)
    {
        InstanciatedObject = externalComponent;
        InstanciatedObject.Initialize(this);

        var gamePlayComponent = ComponentManager.GetComponent<GamePlayComponent>();

        if (gamePlayComponent == null)
        {
            gamePlayComponent = new GamePlayComponent();
            ComponentManager.Add(gamePlayComponent);
            gamePlayComponent.Initialize(this);
        }

        gamePlayComponent.ExternalComponent = InstanciatedObject;
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //FlowGraph.Save(jObject, option);
    }
#endif
}