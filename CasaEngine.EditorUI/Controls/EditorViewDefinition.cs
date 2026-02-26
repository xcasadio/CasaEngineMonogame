using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls;

/// <summary>
/// Parameters for creating a new editor viewport via <see cref="EngineHost.RegisterEditorView"/>.
/// </summary>
public sealed record EditorViewDefinition
{
    /// <summary>Human-readable name shown in debug overlays and logs.</summary>
    public required string Name { get; init; }

    /// <summary>Logical role; controls which camera and overlay components are used.</summary>
    public required EditorViewType ViewType { get; init; }

    /// <summary>Initial width in pixels (the viewport's RenderTargetSurface is created at this size).</summary>
    public int InitialWidth { get; init; } = 1;

    /// <summary>Initial height in pixels.</summary>
    public int InitialHeight { get; init; } = 1;

    /// <summary>
    /// Optional world to render.  When null, a new empty <see cref="World"/> is created
    /// automatically by <see cref="EngineHost.RegisterEditorView"/>.
    /// </summary>
    public World? World { get; init; }

    /// <summary>Background clear color. Default: CornflowerBlue.</summary>
    public Color ClearColor { get; init; } = Color.CornflowerBlue;

    /// <summary>
    /// How often this view is re-rendered.
    /// 3-D views default to <see cref="ViewUpdateMode.RealTime"/>;
    /// 2-D asset-preview views default to <see cref="ViewUpdateMode.OnDemand"/>.
    /// When null the <see cref="EngineHost"/> picks a sensible default based on <see cref="ViewType"/>.
    /// </summary>
    public ViewUpdateMode? UpdateMode { get; init; }

    // ---- Optional editor overlay components ----

    /// <summary>Whether to add a gizmo (translate/rotate/scale) overlay. Ignored for 2-D views.</summary>
    public bool ShowGizmo { get; init; }

    /// <summary>Whether to add a ground-plane grid overlay. Ignored for 2-D views.</summary>
    public bool ShowGrid { get; init; }

    /// <summary>Whether to add an XYZ axis indicator overlay. Ignored for 2-D views.</summary>
    public bool ShowAxis { get; init; }
}
