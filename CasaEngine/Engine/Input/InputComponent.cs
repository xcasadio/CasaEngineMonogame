using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Engine.Input;

public class InputComponent : GameComponent
{
    private IKeyboardStateProvider _keyboardStateProvider;
    private IGamePadStateProvider _gamePadStateProvider;
    private IMouseStateProvider _mouseStateProvider;

    private readonly AxisManager _axisManager = new();
    public InputMappingManager InputMappingManager { get; } = new();

    public MouseManager MouseManager { get; } = new();
    public KeyboardManager KeyboardManager { get; } = new();
    public GamePadManager GamePadManager { get; } = new();

    public InputComponent(Game game)
        : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        Game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.Input;

        SetProviders(new KeyboardStateProvider(), new MouseStateProvider(), new GamePadStateProvider());
    }

    public void SetProviders(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider, IGamePadStateProvider gamePadStateProvider)
    {
        _keyboardStateProvider = keyboardStateProvider;
        _mouseStateProvider = mouseStateProvider;
        _gamePadStateProvider = gamePadStateProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<InputComponent>();
            }
        }

        base.Dispose(disposing);
    }

    public override void Update(GameTime gameTime)
    {
        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        GamePadManager.Update(_gamePadStateProvider, elapsedTime);
        KeyboardManager.Update(_keyboardStateProvider.GetState());
        MouseManager.Update(_mouseStateProvider.GetState());

        _axisManager.Update(KeyboardManager, MouseManager, GamePadManager, elapsedTime);
        InputMappingManager.Update(KeyboardManager, MouseManager, GamePadManager);

        base.Update(gameTime);
    }

#if EDITOR

    public KeyboardState Keyboard => KeyboardManager.State;
    public MouseState MouseState => MouseManager.State;

#endif
}