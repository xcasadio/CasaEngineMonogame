using MGUI.Core.UI;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;

namespace CasaEngine.Editor.Workspaces;

public sealed class UIScreenEditorWorkspace : EditorWorkspaceBase
{
    public UIScreenEditorWorkspace(EditorPanelRegistry panelRegistry)
        : base(panelRegistry)
    {
    }

    protected override EditorPanelScope WorkspaceScope => EditorPanelScope.UIScreen;

    public override EditorWorkspaceId Id => EditorWorkspaceId.UIScreen;

    public override string DisplayName => "UIScreen";

    public override DockNode CreateDefaultLayout()
    {
        var hierarchy = CreatePanelNode(EditorPanelIds.UIScreenHierarchy);
        var toolbox = CreatePanelNode(EditorPanelIds.UIScreenToolbox);
        var inspector = CreatePanelNode(EditorPanelIds.UIScreenInspector);
        var contentBrowser = CreatePanelNode(EditorPanelIds.ContentBrowser);
        var output = CreatePanelNode(EditorPanelIds.Output);

        var bottomGroup = new DockTabGroupNode();
        bottomGroup.AddPanel(contentBrowser, -1);
        bottomGroup.AddPanel(output, -1);
        bottomGroup.SetActivePanel(contentBrowser.Id);

        var documentGroup = new DockTabGroupNode
        {
            IsDocumentArea = true,
        };

        var leftGroup = new DockTabGroupNode();
        leftGroup.AddPanel(hierarchy, -1);
        leftGroup.AddPanel(toolbox, -1);
        leftGroup.SetActivePanel(hierarchy.Id);

        var rightGroup = new DockTabGroupNode();
        rightGroup.AddPanel(inspector, -1);
        rightGroup.SetActivePanel(inspector.Id);

        var centerRightSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = documentGroup,
            SecondChild = rightGroup,
            SplitRatio = 0.72f,
            MinFirstSize = 500,
            MinSecondSize = 260,
        };

        var topAreaSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = leftGroup,
            SecondChild = centerRightSplit,
            SplitRatio = 0.23f,
            MinFirstSize = 260,
            MinSecondSize = 700,
        };

        return new DockSplitNode
        {
            Orientation = Orientation.Vertical,
            FirstChild = topAreaSplit,
            SecondChild = bottomGroup,
            SplitRatio = 0.7f,
            MinFirstSize = 250,
            MinSecondSize = 120,
        };
    }
}