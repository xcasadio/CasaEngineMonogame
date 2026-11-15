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

    /// <summary>
    /// The editor's single preferences store (ADR-0091), shared by every viewport panel built on this adapter.
    /// Null in hosts that do not load preferences.
    /// </summary>
    public EditorPreferencesStore Preferences { get; set; }

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