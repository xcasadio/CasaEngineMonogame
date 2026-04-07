using System;
using System.Linq;
using CasaEngine.Editor.Docking;
using CasaEngine.Editor.Workspaces;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class MaterialEditorWorkspaceTests
{
    [Fact]
    public void CreateDefaultLayout_UsesExpectedShellPanels()
    {
        var builder = new EditorShellLayoutBuilder(CreatePanelRegistry());

        var root = Assert.IsType<DockSplitNode>(builder.CreateDefaultLayout());
        Assert.Equal(Orientation.Vertical, root.Orientation);

        var topArea = Assert.IsType<DockSplitNode>(root.FirstChild);
        Assert.Equal(Orientation.Horizontal, topArea.Orientation);

        var leftGroup = Assert.IsType<DockTabGroupNode>(topArea.FirstChild);
        Assert.Equal(new[] { EditorPanelIds.Hierarchy, EditorPanelIds.Toolbox }, leftGroup.Panels.Select(panel => panel.Id).ToArray());

        var centerRightSplit = Assert.IsType<DockSplitNode>(topArea.SecondChild);
        Assert.Equal(Orientation.Horizontal, centerRightSplit.Orientation);

        var documentGroup = Assert.IsType<DockTabGroupNode>(centerRightSplit.FirstChild);
        Assert.True(documentGroup.IsDocumentArea);
        var worldViewportPanel = Assert.Single(documentGroup.Panels);
        Assert.Equal(EditorPanelIds.WorldViewport, worldViewportPanel.Id);
        Assert.False(worldViewportPanel.CanClose);
        Assert.Equal(DockableType.Document, worldViewportPanel.DockableType);

        var rightGroup = Assert.IsType<DockTabGroupNode>(centerRightSplit.SecondChild);
        var inspectorPanel = Assert.Single(rightGroup.Panels);
        Assert.Equal(EditorPanelIds.Inspector, inspectorPanel.Id);
        Assert.Equal(DockableType.Tool, inspectorPanel.DockableType);

        var bottomGroup = Assert.IsType<DockTabGroupNode>(root.SecondChild);
        Assert.Equal(new[] { EditorPanelIds.ContentBrowser, EditorPanelIds.Output }, bottomGroup.Panels.Select(panel => panel.Id).ToArray());
    }

    private static EditorPanelRegistry CreatePanelRegistry()
    {
        return new EditorPanelRegistry(new[]
        {
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.WorldViewport,
                Title = "World Viewport",
                Kind = EditorPanelKind.Document,
                CanClose = false,
                CanFloat = false,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Hierarchy,
                Title = "Hierarchy",
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Inspector,
                Title = "Inspector",
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Toolbox,
                Title = "Toolbox",
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.ContentBrowser,
                Title = "Content Browser",
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Output,
                Title = "Output / Logs",
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
        });
    }

    private static MGElement CreateUnavailableContent()
        => throw new NotSupportedException("The content factory should not be invoked in layout tests.");
}