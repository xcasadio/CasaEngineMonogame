using CasaEngine.Engine.Physics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Scripts.Screens;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptTitleScreenWorld : GameplayProxy
{
    private TitleScreen? _titleScreen;

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
        if (world.Game.GameManager.ViewManager.GetActiveUIView() == null) return;

        void OnStartGame() => world.Game.GameManager.SetWorldToLoad("DefaultWorld.world");
        void OnExit()      => world.Game.Exit();

        _titleScreen = new TitleScreen(OnStartGame, OnExit);
        world.Game.GameManager.ScreenManager.PushScreenToActiveView(_titleScreen);
    }

    public override void OnEndPlay(World world)
    {
        if (_titleScreen != null)
        {
            world.Game.GameManager.ScreenManager.RemoveScreenFromActiveView(_titleScreen);
        }

        _titleScreen = null;
    }

    public override IGameplayProxy Clone()
    {
        return new ScriptTitleScreenWorld();
    }
}