using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Controllers;

namespace CasaEngine.RPGDemo.Components;

public abstract class CharacterComponent : Component
{
    public Character Character { get; }
    public Controller Controller { get; protected init; }

    protected CharacterComponent(Entity owner, int componentId) : base(owner, componentId)
    {
        Character = new Character(owner);
    }

    public override void Initialize(CasaEngineGame game)
    {
        Controller.Initialize(game);
    }

    public override void Update(float elapsedTime)
    {
        Controller.Update(elapsedTime);
    }

    public override void Draw()
    {

    }
}