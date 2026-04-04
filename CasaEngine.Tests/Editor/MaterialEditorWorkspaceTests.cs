using System;
using System.Linq;
using CasaEngine.Editor.Workspaces;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class MaterialEditorWorkspaceTests
{
    [Fact]
    public void Panels_ReturnsCommonAndMaterialPanelsOnly()
    {
        var workspace = new MaterialEditorWorkspace(CreatePanelRegistry());

        var panelIds = workspace.Panels.Select(descriptor => descriptor.Id).ToArray();

        Assert.Contains(EditorPanelIds.MaterialDetails, panelIds);
        Assert.Contains(EditorPanelIds.ContentBrowser, panelIds);
        Assert.Contains(EditorPanelIds.Output, panelIds);
        Assert.DoesNotContain(EditorPanelIds.Entities, panelIds);
        Assert.DoesNotContain(EditorPanelIds.EntityDetails, panelIds);
    }

    [Fact]
    public void CreateDefaultLayout_BuildsDocumentDetailsAndBottomPanels()
    {
        var workspace = new MaterialEditorWorkspace(CreatePanelRegistry());

        var root = Assert.IsType<DockSplitNode>(workspace.CreateDefaultLayout());
        Assert.Equal(Orientation.Vertical, root.Orientation);

        var topArea = Assert.IsType<DockSplitNode>(root.FirstChild);
        Assert.Equal(Orientation.Horizontal, topArea.Orientation);

        var documentGroup = Assert.IsType<DockTabGroupNode>(topArea.FirstChild);
        Assert.True(documentGroup.IsDocumentArea);
        Assert.Empty(documentGroup.Panels);

        var detailsGroup = Assert.IsType<DockTabGroupNode>(topArea.SecondChild);
        var detailsPanel = Assert.Single(detailsGroup.Panels);
        Assert.Equal(EditorPanelIds.MaterialDetails, detailsPanel.Id);

        var bottomArea = Assert.IsType<DockSplitNode>(root.SecondChild);
        Assert.Equal(Orientation.Horizontal, bottomArea.Orientation);

        var contentBrowserGroup = Assert.IsType<DockTabGroupNode>(bottomArea.FirstChild);
        var contentBrowserPanel = Assert.Single(contentBrowserGroup.Panels);
        Assert.Equal(EditorPanelIds.ContentBrowser, contentBrowserPanel.Id);

        var outputGroup = Assert.IsType<DockTabGroupNode>(bottomArea.SecondChild);
        var outputPanel = Assert.Single(outputGroup.Panels);
        Assert.Equal(EditorPanelIds.Output, outputPanel.Id);
    }

    private static EditorPanelRegistry CreatePanelRegistry()
    {
        return new EditorPanelRegistry(new[]
        {
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.MaterialDetails,
                Title = "Details",
                Scope = EditorPanelScope.Material,
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.ContentBrowser,
                Title = "Content Browser",
                Scope = EditorPanelScope.Common,
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Output,
                Title = "Output / Logs",
                Scope = EditorPanelScope.Common,
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.Entities,
                Title = "Entities",
                Scope = EditorPanelScope.World,
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
            new EditorPanelDescriptor
            {
                Id = EditorPanelIds.EntityDetails,
                Title = "Details",
                Scope = EditorPanelScope.World,
                Kind = EditorPanelKind.Tool,
                ContentFactory = CreateUnavailableContent,
            },
        });
    }

    private static MGElement CreateUnavailableContent()
        => throw new NotSupportedException("The content factory should not be invoked in layout tests.");
}