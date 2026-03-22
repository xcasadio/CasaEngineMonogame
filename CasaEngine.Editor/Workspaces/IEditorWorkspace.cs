using System.Collections.Generic;
using MGUI.Core.UI.Docking.DockLayout;

namespace CasaEngine.Editor.Workspaces;

public interface IEditorWorkspace
{
    EditorWorkspaceId Id { get; }

    string DisplayName { get; }

    IReadOnlyList<EditorPanelDescriptor> Panels { get; }

    DockNode CreateDefaultLayout();

    bool SupportsPanel(string panelId);
}