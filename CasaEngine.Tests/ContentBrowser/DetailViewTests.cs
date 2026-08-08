using CasaEngine.Editor.ContentBrowser.Models;
using CasaEngine.Editor.ContentBrowser.Views;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public class DetailViewTests
{
    [Fact]
    public void DoubleClickingARow_RaisesFileDoubleClicked()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        DetailView detailView = CreateDetailView(harness);

        List<ContentItem> items = new()
        {
            new ContentItem(@"D:\TestAssets\hero.png", false),
            new ContentItem(@"D:\TestAssets\villain.png", false),
        };
        detailView.SetItems(items);

        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        ContentItem? openedItem = null;
        detailView.FileDoubleClicked += item => openedItem = item;

        Point rowCenter = GetFirstRowCenter(detailView);
        harness.AdvanceFrame(32, rowCenter);

        int elapsed = harness.Click(48, rowCenter);
        harness.Click(elapsed, rowCenter);

        Assert.NotNull(openedItem);
        Assert.Equal(items[0].FullPath, openedItem!.FullPath);
    }

    [Fact]
    public void ClickingTheSelectedRowAgain_KeepsItSelected()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        DetailView detailView = CreateDetailView(harness);

        detailView.SetItems(new List<ContentItem> { new(@"D:\TestAssets\hero.png", false) });

        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        Point rowCenter = GetFirstRowCenter(detailView);
        harness.AdvanceFrame(32, rowCenter);

        int elapsed = harness.Click(48, rowCenter);
        Assert.Single(detailView.SelectedItems);

        //  Re-clicking must not toggle the selection off: the grid view behaves the same way and the
        //  double-click handler depends on the row still being selected.
        harness.Click(elapsed + 1000, rowCenter);
        Assert.Single(detailView.SelectedItems);
    }

    private static DetailView CreateDetailView(ContentBrowserViewTestHarness harness)
    {
        DetailView detailView = new(harness.Window, static _ => null);
        harness.Window.SetContent(detailView.RootElement);
        return detailView;
    }

    private static Point GetFirstRowCenter(DetailView detailView)
    {
        Assert.NotEmpty(detailView.ListView.RowItems);
        MGListViewItem<ContentItem> rowItem = detailView.ListView.RowItems[0];
        Dictionary<MGListViewColumn<ContentItem>, MGElement> rowContents = rowItem.GetRowContents();
        foreach (MGElement cell in rowContents.Values)
        {
            if (!cell.ActualLayoutBounds.IsEmpty)
            {
                return cell.ActualLayoutBounds.Center;
            }
        }

        Assert.Fail("No realized cell was found for the first row.");
        return Point.Zero;
    }
}
