using MGUI.Core.UI;
using MGUI.Core.UI.Docking.DockLayout;
using CasaEngine.Editor.Workspaces;

namespace CasaEngine.Editor.Docking;

public sealed class EditorShellLayoutBuilder
{
    private readonly EditorPanelRegistry _panelRegistry;

    public EditorShellLayoutBuilder(EditorPanelRegistry panelRegistry)
    {
        _panelRegistry = panelRegistry;
    }

    public DockNode CreateDefaultLayout()
    {
        var worldViewport = CreatePanelNode(EditorPanelIds.WorldViewport);
        var hierarchy = CreatePanelNode(EditorPanelIds.Hierarchy);
        var inspector = CreatePanelNode(EditorPanelIds.Inspector);
        var toolbox = CreatePanelNode(EditorPanelIds.Toolbox);
        var contentBrowser = CreatePanelNode(EditorPanelIds.ContentBrowser);
        var output = CreatePanelNode(EditorPanelIds.Output);
        var animation2dTimeline = CreatePanelNode(EditorPanelIds.Animation2dTimeline);

        var documentGroup = new DockTabGroupNode
        {
            IsDocumentArea = true,
        };
        documentGroup.AddPanel(worldViewport, -1);
        documentGroup.SetActivePanel(worldViewport.Id);

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
            SplitRatio = 0.74f,
            MinFirstSize = 520,
            MinSecondSize = 280,
        };

        var topAreaSplit = new DockSplitNode
        {
            Orientation = Orientation.Horizontal,
            FirstChild = leftGroup,
            SecondChild = centerRightSplit,
            SplitRatio = 0.22f,
            MinFirstSize = 240,
            MinSecondSize = 720,
        };

        var bottomGroup = new DockTabGroupNode();
        bottomGroup.AddPanel(contentBrowser, -1);
        bottomGroup.AddPanel(animation2dTimeline, -1);
        bottomGroup.AddPanel(output, -1);
        bottomGroup.SetActivePanel(contentBrowser.Id);

        return new DockSplitNode
        {
            Orientation = Orientation.Vertical,
            FirstChild = topAreaSplit,
            SecondChild = bottomGroup,
            SplitRatio = 0.72f,
            MinFirstSize = 260,
            MinSecondSize = 120,
        };
    }

    private DockPanelNode CreatePanelNode(string panelId)
    {
        var descriptor = _panelRegistry.GetDescriptor(panelId);
        return new DockPanelNode(descriptor.Id)
        {
            Title = descriptor.Title,
            DockableType = descriptor.Kind == EditorPanelKind.Document ? DockableType.Document : DockableType.Tool,
            CanClose = descriptor.CanClose,
            CanFloat = descriptor.CanFloat,
            CanAutoHide = descriptor.CanAutoHide,
            ContentFactory = descriptor.ContentFactory,
        };
    }
}