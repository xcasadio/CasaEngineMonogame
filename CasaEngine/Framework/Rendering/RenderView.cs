using Microsoft.Xna.Framework;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Describes a render view: a combination of world, camera and output surface.
/// </summary>
public sealed class RenderView
{
    /// <summary>World to render in this view.</summary>
    public World.World World { get; set; }

    /// <summary>Camera used to compute View/Projection matrices.</summary>
    public CameraComponent Camera { get; set; }

    /// <summary>Output surface (backbuffer or RenderTarget).</summary>
    public IRenderSurface Surface { get; set; }

    /// <summary>Clear color.</summary>
    public Color ClearColor { get; set; } = Color.CornflowerBlue;

    /// <summary>If true, clears the color buffer before rendering.</summary>
    public bool ClearColorBuffer { get; set; } = true;

    /// <summary>If true, clears the depth/stencil buffer before rendering.</summary>
    public bool ClearDepthBuffer { get; set; } = true;

    /// <summary>Optional name for debugging purposes.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>If false, the view is skipped by the pipeline.</summary>
    public bool Enabled { get; set; } = true;

    public RenderView(World.World world, CameraComponent camera, IRenderSurface surface)
    {
        World   = world;
        Camera  = camera;
        Surface = surface;
    }
}
