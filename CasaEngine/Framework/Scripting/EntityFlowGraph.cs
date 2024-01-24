#if EDITOR
#endif

namespace CasaEngine.Framework.Scripting;
/*
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

    public GameplayProxy InstanciatedObject { get; private set; }

    public void InitializeScript(GameplayProxy externalComponent)
    {
        InstanciatedObject = externalComponent;
        InstanciatedObject.LoadContent(this);

        var gamePlayComponent = ComponentManager.GetComponent<GamePlayComponent>();

        if (gamePlayComponent == null)
        {
            gamePlayComponent = new GamePlayComponent();
            ComponentManager.Add(gamePlayComponent);
            gamePlayComponent.LoadContent(this);
        }

        gamePlayComponent.GameplayProxy = InstanciatedObject;
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //FlowGraph.Save(jObject, option);
    }
#endif
}*/