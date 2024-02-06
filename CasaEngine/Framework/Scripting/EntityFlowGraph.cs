#if EDITOR
#endif

namespace CasaEngine.Framework.Scripting;
/*
public class EntityFlowGraph : Entity
{
    public override void Load(JsonElement element)
    {
        base.Load(element);

        //FlowGraph.Load(element.GetElement("flow_graph"));
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

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        //FlowGraph.Save(jObject);
    }
#endif
}*/