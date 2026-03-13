namespace CasaEngine.Framework.Game.Components.Editor;

/// <summary>
/// Identifies the logical role of an editor viewport.
/// Used by <see cref="EditorViewContext"/> and <c>EngineHost.RegisterEditorView()</c>
/// to configure the correct camera type, overlay components, and update mode.
/// </summary>
public enum EditorViewType
{
    /// <summary>Full 3-D world editing: gizmo, grid, axis, drag-drop entities.</summary>
    World,

    /// <summary>Single-entity 3-D preview: gizmo, grid, axis, no world loading.</summary>
    Entity,

    /// <summary>2-D sprite preview with pan / zoom camera. No gizmo or grid.</summary>
    Sprite,

    /// <summary>2-D animation playback with pan / zoom camera.</summary>
    Animation2d,

    /// <summary>2-D tile-map editor with pan / zoom camera and paint tools.</summary>
    TileMap,

    /// <summary>Custom viewport — caller supplies its own components and camera.</summary>
    Custom,
}
