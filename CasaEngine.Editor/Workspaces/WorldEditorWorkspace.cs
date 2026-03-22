using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;

namespace CasaEngine.Editor.Workspaces;

public sealed class WorldEditorWorkspace : EditorWorkspaceBase
{
    public WorldEditorWorkspace(EditorPanelRegistry panelRegistry)
        : base(panelRegistry)
    {
    }

    protected override EditorPanelScope WorkspaceScope => EditorPanelScope.World;

    public override EditorWorkspaceId Id => EditorWorkspaceId.World;

    public override string DisplayName => "World";

    public override DockNode CreateDefaultLayout()
    {
        var worldViewport = CreatePanelNode(EditorPanelIds.WorldViewport);
        var entities = CreatePanelNode(EditorPanelIds.Entities);
        var details = CreatePanelNode(EditorPanelIds.EntityDetails);
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
        documentGroup.AddPanel(worldViewport, -1);
        documentGroup.SetActivePanel(worldViewport.Id);

        var entitiesGroup = new DockTabGroupNode();
        entitiesGroup.AddPanel(entities, -1);
        entitiesGroup.SetActivePanel(entities.Id);

        var detailsGroup = new DockTabGroupNode();
        detailsGroup.AddPanel(details, -1);
        detailsGroup.SetActivePanel(details.Id);

        var centerRightSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = documentGroup,
            SecondChild = detailsGroup,
            SplitRatio = 0.72f,
            MinFirstSize = 500,
            MinSecondSize = 260,
        };

        var topAreaSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = entitiesGroup,
            SecondChild = centerRightSplit,
            SplitRatio = 0.2f,
            MinFirstSize = 220,
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