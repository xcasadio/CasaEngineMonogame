using System;
using CasaEngine.Engine.Input.Providers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application.Components.DebugTools;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Editor.Runtime;

public sealed class EditorViewContext : IDisposable
{
    private bool _disposed;

    public ViewId ViewId { get; }

    public RenderView RenderView { get; }

    public Framework.Scene.World.World? World { get; set; }

    public CameraComponent? Camera { get; set; }

    public Entity? CameraEntity { get; set; }

    public RenderTargetSurface? Surface { get; set; }

    public TransformGizmoComponent? Gizmo { get; set; }

    public GridComponent? Grid { get; set; }

    public AxisComponent? Axis { get; set; }

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