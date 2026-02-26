using System.Windows;
using CasaEngine.EditorUI.DragAndDrop;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Must be set before the first D3D11Host (GameEditor) control is created.
        // All editor viewports will share a single GraphicsDevice, which is the
        // prerequisite for sharing GPU resources (textures, models, render targets)
        // across editor tabs — the foundation for PR3+ of the multi-view migration.
        D3D11Host.UseASingleSharedGraphicsDevice = true;

        DragAndDropConfiguration.RegisterAllHandlers();

        base.OnStartup(e);
    }
}