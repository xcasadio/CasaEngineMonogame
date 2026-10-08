using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// How the demo browser's tree loads a demo (decision D2 of `ai-agent/tasks/demo-browser-split-tasks.md`): a click on a
/// demo loads it, even when that demo is already selected, and the arrows only move the selection. MGTreeView raises
/// <see cref="MGTreeView.SelectionChanged"/> only when the selection changes, so the browser listens to the click of each
/// leaf instead. These tests build the tree the way <c>DemoBrowserScreen</c> does, on the real `demo-browser.xaml` and a
/// headless desktop, and pin the MGUI behaviour that choice relies on (the screen itself lives in the demos app, which
/// this project does not reference).
/// </summary>
public class DemoBrowserTreeTests
{
    private static readonly Point PointerAway = new(-100, -100);

    private sealed class Browser
    {
        public MGDesktop Desktop;
        public HeadlessUiTestHarness.HeadlessRuntime Runtime;
        public MGTreeView Tree;
        public MGTreeViewItem Theme;
        public MGTreeViewItem First;
        public MGTreeViewItem Second;
        public int Frame;
    }

    private static Browser LoadBrowserWithTwoDemos()
    {
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(280, 768);
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(DemoScreenXamlTests.ScreenPath("demo-browser.xaml")));
        desktop.Windows.Add(window);
        Assert.True(window.TryGetElementByName("treeDemos", out MGTreeView tree));

        var theme = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Rendering", null, null, false) };
        var first = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Static model demo", null, null, false) };
        var second = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Material system demo", null, null, false) };
        theme.AddItem(first);
        theme.AddItem(second);
        tree.AddItem(theme);
        theme.IsExpanded = true;

        var browser = new Browser { Desktop = desktop, Runtime = runtime, Tree = tree, Theme = theme, First = first, Second = second };
        Step(browser, PointerAway, ButtonState.Released, new KeyboardState());
        Step(browser, PointerAway, ButtonState.Released, new KeyboardState());
        return browser;
    }

    private static void Step(Browser browser, Point pointer, ButtonState left, KeyboardState keyboard)
    {
        browser.Frame++;
        browser.Runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(16 * browser.Frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(pointer.X, pointer.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            keyboard));
        browser.Desktop.Update();
    }

    private static Point HeaderCentre(MGTreeViewItem item)
    {
        var header = item.HeaderContent;
        return header.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, header.LayoutBounds.Center);
    }

    private static void Click(Browser browser, MGTreeViewItem item)
    {
        var centre = HeaderCentre(item);
        Step(browser, centre, ButtonState.Released, new KeyboardState());
        Step(browser, centre, ButtonState.Pressed, new KeyboardState());
        Step(browser, centre, ButtonState.Released, new KeyboardState());
        // Move away so that the next click on the same leaf is a new click, not a double click.
        Step(browser, PointerAway, ButtonState.Released, new KeyboardState());
    }

    [Fact]
    public void ClickingALeaf_ReachesItsClickHandlerAndSelectsIt()
    {
        var browser = LoadBrowserWithTwoDemos();
        int clicks = 0;
        browser.Second.MouseHandler.LMBClickedInside += (_, _) => clicks++;

        Click(browser, browser.Second);

        Assert.Equal(1, clicks);
        Assert.Same(browser.Second, browser.Tree.SelectedItem);
    }

    [Fact]
    public void ClickingTheSelectedLeafAgain_ReachesItsClickHandlerButNotSelectionChanged()
    {
        // The reason the browser loads on the click of the leaf: the second click raises no SelectionChanged.
        var browser = LoadBrowserWithTwoDemos();
        int clicks = 0;
        int selectionChanges = 0;
        browser.Second.MouseHandler.LMBClickedInside += (_, _) => clicks++;
        browser.Tree.SelectionChanged += (_, _) => selectionChanges++;

        Click(browser, browser.Second);
        for (int i = 0; i < 20; i++)
        {
            Step(browser, PointerAway, ButtonState.Released, new KeyboardState());
        }

        Click(browser, browser.Second);

        Assert.Equal(2, clicks);
        Assert.Equal(1, selectionChanges);
    }

    [Fact]
    public void SelectItem_RaisesSelectionChangedWithoutAnyClick()
    {
        // How the browser shows the current demo after a load: programmatic selection updates the description through
        // SelectionChanged, and no click handler runs, so nothing is loaded twice.
        var browser = LoadBrowserWithTwoDemos();
        int clicks = 0;
        MGTreeViewItem selected = null;
        browser.First.MouseHandler.LMBClickedInside += (_, _) => clicks++;
        browser.Tree.SelectionChanged += (_, item) => selected = item;

        browser.Tree.SelectItem(browser.First);

        Assert.Same(browser.First, selected);
        Assert.Same(browser.First, browser.Tree.SelectedItem);
        Assert.Equal(0, clicks);
    }
}
