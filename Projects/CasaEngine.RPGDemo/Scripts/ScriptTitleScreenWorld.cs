using System.Linq;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Scripts.Screens;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptTitleScreenWorld : GameplayProxy
{
    public override void InitializeWithWorld(World world)
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

    public override void OnBeginPlay(World world)
    {
        var uiRoot = world.Game.GameManager.ViewManager.Views
            .FirstOrDefault(v => v.UIRoot != null)?.UIRoot;
        if (uiRoot == null) return;

        void OnStartGame() => world.Game.GameManager.SetWorldToLoad("DefaultWorld.world");
        void OnExit()      => world.Game.Exit();

        uiRoot.PushScreen(new TitleScreen(OnStartGame, OnExit));
    }

    public override void OnEndPlay(World world)
    {
        var uiRoot = world.Game.GameManager.ViewManager.Views
            .FirstOrDefault(v => v.UIRoot != null)?.UIRoot;
        uiRoot?.ScreenStack.Clear();
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptTitleScreenWorld();
    }
}