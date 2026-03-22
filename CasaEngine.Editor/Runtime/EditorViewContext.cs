using System;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.DebugTools;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game.Components.Editor;

public sealed class EditorViewContext : IDisposable
{
    private bool _disposed;

    public ViewId ViewId { get; }

    public RenderView RenderView { get; }

    public World.World? World { get; set; }

    public CameraComponent? Camera { get; set; }

    public Entity? CameraEntity { get; set; }

    public RenderTargetSurface? Surface { get; set; }

    public TransformGizmoComponent? Gizmo { get; set; }

    public DebugGridComponent? Grid { get; set; }

    public DebugAxisComponent? Axis { get; set; }

    public IKeyboardStateProvider? KeyboardProvider { get; set; }

    public IMouseStateProvider? MouseProvider { get; set; }

    public string Name { get; set; }

    public EditorViewType ViewType { get; set; }

    public EditorViewContext(
        ViewId viewId,
        RenderView renderView,
        string name,
        EditorViewType viewType)
    {
        ViewId = viewId;
        RenderView = renderView;
        Name = name;
        ViewType = viewType;
        renderView.Tag = this;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Surface?.Dispose();
        Surface = null;

        if (RenderView.Tag == this)
        {
            RenderView.Tag = null;
        }
    }
}