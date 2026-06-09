using System;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.ContextualPanels;

public sealed class ContextualPanelDefinition
{
    public required EditorPanelRole Role { get; init; }

    public required EditorDocumentKind DocumentKind { get; init; }

    public required string Title { get; init; }

    public required Func<MGElement> ContentFactory { get; init; }

    public Action<EditorContextService> Refresh { get; init; }
}