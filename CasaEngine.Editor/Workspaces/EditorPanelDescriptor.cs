using System;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Workspaces;

public sealed class EditorPanelDescriptor
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required EditorPanelKind Kind { get; init; }

    public required Func<MGElement> ContentFactory { get; init; }

    public bool CanClose { get; init; } = true;

    public bool CanFloat { get; init; } = true;

    public bool CanAutoHide { get; init; }
}