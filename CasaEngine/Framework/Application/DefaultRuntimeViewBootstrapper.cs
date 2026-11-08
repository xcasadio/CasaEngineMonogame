using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application;

/// <summary>
/// Creates the default full-screen backbuffer view used by the runtime when no custom views were registered.
/// </summary>
public sealed class DefaultRuntimeViewBootstrapper : IRuntimeViewBootstrapper
{
    public static DefaultRuntimeViewBootstrapper Instance { get; } = new();

    private DefaultRuntimeViewBootstrapper()
    {
    }

    public void BootstrapViews(CasaEngineGame game, Scene.World.World world, ViewManager viewManager)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(viewManager);

        if (viewManager.Views.Count > 0)
        {
            return;
        }

        var camera = world.Entities.Select(static entity => entity.GetComponent<CameraComponent>())
            .FirstOrDefault(static cameraComponent => cameraComponent != null);

        if (camera == null)
        {
            camera = world.CreateDefaultCamera();
        }

        CreateDefaultView(
            world, viewManager, camera, game.ScreenSizeWidth, game.ScreenSizeHeight, game.ActiveVirtualResolution, game.GraphicsDevice);
    }

    /// <summary>
    /// Creates the default view over the layout area of <paramref name="viewManager"/> (the whole window unless it has
    /// layout insets, ADR-0070), or -- with a virtual resolution -- over its integer-fit image (ADR-0048), the camera
    /// framing exactly the virtual resolution. With a buffered mode (ADR-0087) the view renders into a target of the
    /// virtual size instead, its camera at zoom 1, and a <see cref="VirtualResolutionPresenter"/> that owns the target
    /// shows it in the window. Fitted before the view is registered so the UI runtime created on registration already
    /// sees the final rectangle.
    /// </summary>
    /// <param name="graphicsDevice">Needed only by a buffered mode, which creates a render target.</param>
    internal static ViewId CreateDefaultView(
        Scene.World.World world,
        ViewManager viewManager,
        CameraComponent camera,
        int windowWidth,
        int windowHeight,
        VirtualResolutionSettings virtualResolution,
        GraphicsDevice graphicsDevice = null)
    {
        var layoutArea = viewManager.GetLayoutArea(windowWidth, windowHeight);

        if (virtualResolution != null && virtualResolution.Mode.IsBuffered())
        {
            VirtualResolutionRuntime.ThrowIfLayoutAreaIsNotTheWindow(layoutArea, windowWidth, windowHeight);
            ArgumentNullException.ThrowIfNull(graphicsDevice);

            var target = new RenderTargetSurface(graphicsDevice, virtualResolution.Width, virtualResolution.Height);
            var presenter = new VirtualResolutionPresenter(
                target, virtualResolution.Mode, virtualResolution.Width, virtualResolution.Height, windowWidth, windowHeight);
            VirtualResolutionRuntime.FrameBufferedCamera(camera, presenter);

            return viewManager.CreateView(new ViewDefinition
            {
                World = world,
                Camera = camera,
                Surface = target,
                Presenter = presenter,
                Name = "Default view",
                ClearColor = Color.CornflowerBlue,
            });
        }

        var surface = new BackBufferSurface(layoutArea);
        if (virtualResolution != null)
        {
            VirtualResolutionRuntime.ThrowIfLayoutAreaIsNotTheWindow(layoutArea, windowWidth, windowHeight);
            VirtualResolutionLayout.Apply(surface, camera, windowWidth, windowHeight, virtualResolution);
        }
        else if (layoutArea != new Rectangle(0, 0, windowWidth, windowHeight))
        {
            // The world sized the camera to the window; the view only gets the area.
            camera.OnScreenResized(layoutArea.Width, layoutArea.Height);
        }

        return viewManager.CreateView(new ViewDefinition
        {
            World = world,
            Camera = camera,
            Surface = surface,
            Name = "Default view",
            ClearColor = Color.CornflowerBlue,
        });
    }
}