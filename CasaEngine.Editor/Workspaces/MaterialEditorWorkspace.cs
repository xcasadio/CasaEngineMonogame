using MGUI.Core.UI;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;

namespace CasaEngine.Editor.Workspaces;

public sealed class MaterialEditorWorkspace : EditorWorkspaceBase
{
    public MaterialEditorWorkspace(EditorPanelRegistry panelRegistry)
        : base(panelRegistry)
    {
    }

    protected override EditorPanelScope WorkspaceScope => EditorPanelScope.Material;

    public override EditorWorkspaceId Id => EditorWorkspaceId.Material;

    public override string DisplayName => "Material";

    public override DockNode CreateDefaultLayout()
    {
        var worldViewport = CreatePanelNode(EditorPanelIds.WorldViewport);
        var details = CreatePanelNode(EditorPanelIds.MaterialDetails);
        var contentBrowser = CreatePanelNode(EditorPanelIds.ContentBrowser);
        var output = CreatePanelNode(EditorPanelIds.Output);

        var contentBrowserGroup = new DockTabGroupNode();
        contentBrowserGroup.AddPanel(contentBrowser, -1);
        contentBrowserGroup.SetActivePanel(contentBrowser.Id);

        var outputGroup = new DockTabGroupNode();
        outputGroup.AddPanel(output, -1);
        outputGroup.SetActivePanel(output.Id);

        var documentGroup = new DockTabGroupNode
        {
            IsDocumentArea = true,
        };
        documentGroup.AddPanel(worldViewport, -1);
        documentGroup.SetActivePanel(worldViewport.Id);

        var detailsGroup = new DockTabGroupNode();
        detailsGroup.AddPanel(details, -1);
        detailsGroup.SetActivePanel(details.Id);

        var topAreaSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = documentGroup,
            SecondChild = detailsGroup,
            SplitRatio = 0.74f,
            MinFirstSize = 500,
            MinSecondSize = 300,
        };

        var bottomAreaSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = contentBrowserGroup,
            SecondChild = outputGroup,
            SplitRatio = 0.62f,
            MinFirstSize = 320,
            MinSecondSize = 260,
        };

        return new DockSplitNode
        {
            Orientation = Orientation.Vertical,
            FirstChild = topAreaSplit,
            SecondChild = bottomAreaSplit,
            SplitRatio = 0.72f,
            MinFirstSize = 250,
            MinSecondSize = 120,
        };
    }
}