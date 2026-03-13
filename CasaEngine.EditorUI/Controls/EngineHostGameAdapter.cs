using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls;

internal sealed class EngineHostGameAdapter : CasaEngineGame
{
    public EngineHostGameAdapter(
        string? projectFileName = null,
        IGraphicsDeviceService? graphicsDeviceService = null,
        EngineRuntimeContext? runtimeContext = null)
        : base(projectFileName, graphicsDeviceService, runtimeContext)
    {
    }

    public void InitializeHost()
    {
        Initialize();
    }

    public void LoadContentHost()
    {
        LoadContent();
    }

    public void UpdateHost(GameTime gameTime)
    {
        Update(gameTime);
    }

    public void DrawHost(GameTime gameTime)
    {
        Draw(gameTime);
    }

    public void ConfigureFallbackInput(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        InputComponent.SetFallbackProviders(keyboardStateProvider, mouseStateProvider);
    }
}