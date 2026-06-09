using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime;

internal sealed class HostedEditorGameAdapter : CasaEngineGame
{
    public HostedEditorGameAdapter(
        string projectFileName = null,
        IGraphicsDeviceService graphicsDeviceService = null,
        EngineRuntimeContext runtimeContext = null)
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
}