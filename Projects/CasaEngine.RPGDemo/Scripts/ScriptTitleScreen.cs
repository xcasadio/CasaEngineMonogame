using CasaEngine.Engine.Physics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using TomShane.Neoforce.Controls;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptTitleScreen : GameplayProxy
{
    private ScreenGui _screen;
    private Button _startGameButton;
    private Button _exitButton;

    public override void InitializeWithWorld(World world)
    {
        _screen = Owner as ScreenGui;
        _startGameButton = (Button)_screen.GetControlByName("ButtonStartGame");
        _exitButton = (Button)_screen.GetControlByName("ButtonExit");

        _startGameButton.Click += StartGameButton_Click;
        _exitButton.Click += ExitButton_Click;
    }

    private void StartGameButton_Click(object sender, EventArgs e)
    {
        _screen.World.Game.GameManager.SetWorldToLoad("DefaultWorld.world");
    }

    private void ExitButton_Click(object sender, EventArgs e)
    {
        _screen.World.Game.Exit();
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

    }

    public override void OnEndPlay(World world)
    {

    }

    public override ScriptMainHUDScreen Clone()
    {
        return new ScriptMainHUDScreen();
    }
}