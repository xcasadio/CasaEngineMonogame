using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// How the demos main screen (`ai-agent/tasks/demos-main-menu-tasks.md`, decision D3) is driven, on the real
/// `main-menu.xaml` and a headless desktop, with the tree built the way <c>MainMenuScreen</c> builds it: the arrows move
/// the selection of the focused tree and the sheet follows <see cref="MGTreeView.SelectionChanged"/>; a double click on a
/// demo reaches the demo's own double-click handler; the Launch and Quit buttons run their commands; Enter and Escape on
/// the tree neither change the selection nor do anything else, which is why the game reads Enter (and A) itself and why
/// nothing quits from this screen (decision D4). These pin the MGUI behaviours the screen relies on; the screen itself,
/// linked from the demos app, is tested by <see cref="MainMenuScreenTests"/>.
/// </summary>
public class DemoMainMenuTests
{
    private static readonly Point PointerAway = new(-100, -100);

    private sealed class Menu
    {
        public MGDesktop Desktop;
        public HeadlessUiTestHarness.HeadlessRuntime Runtime;
        public MGWindow Window;
        public MGTreeView Tree;
        public MGTreeViewItem Theme;
        public MGTreeViewItem First;
        public MGTreeViewItem Second;
        public int Frame;
    }

    private static Menu LoadMenuWithTwoDemos()
    {
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(DemoScreenXamlTests.ScreenPath("main-menu.xaml")));
        desktop.Windows.Add(window);
        Assert.True(window.TryGetElementByName("treeDemos", out MGTreeView tree));

        var theme = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Rendering", null, null, false) };
        var first = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Static model demo", null, null, false) };
        var second = new MGTreeViewItem(window) { Header = new MGTextBlock(window, "Material system demo", null, null, false) };
        theme.AddItem(first);
        theme.AddItem(second);
        tree.AddItem(theme);
        theme.IsExpanded = true;

        var menu = new Menu { Desktop = desktop, Runtime = runtime, Window = window, Tree = tree, Theme = theme, First = first, Second = second };
        Step(menu, PointerAway, ButtonState.Released, new KeyboardState());
        Step(menu, PointerAway, ButtonState.Released, new KeyboardState());
        return menu;
    }

    private static void Step(Menu menu, Point pointer, ButtonState left, KeyboardState keyboard)
    {
        menu.Frame++;
        menu.Runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(16 * menu.Frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(pointer.X, pointer.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            keyboard));
        menu.Desktop.Update();
    }

    private static void PressKey(Menu menu, Keys key)
    {
        Step(menu, PointerAway, ButtonState.Released, new KeyboardState(key));
        Step(menu, PointerAway, ButtonState.Released, new KeyboardState());
    }

    private static Point CentreOf(MGElement element)
        => element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, element.LayoutBounds.Center);

    private static void Click(Menu menu, Point point)
    {
        Step(menu, point, ButtonState.Released, new KeyboardState());
        Step(menu, point, ButtonState.Pressed, new KeyboardState());
        Step(menu, point, ButtonState.Released, new KeyboardState());
    }

    private static void SelectAndFocus(Menu menu, MGTreeViewItem item)
    {
        menu.Tree.SelectItem(item);
        menu.Tree.Focus(KeyboardFocusSource.Programmatic);
        Step(menu, PointerAway, ButtonState.Released, new KeyboardState());
    }

    [Fact]
    public void TheArrows_MoveTheSelectionOfTheFocusedTree_AndRaiseSelectionChanged()
    {
        var menu = LoadMenuWithTwoDemos();
        SelectAndFocus(menu, menu.First);
        MGTreeViewItem shown = null;
        menu.Tree.SelectionChanged += (_, item) => shown = item;

        PressKey(menu, Keys.Down);

        Assert.Same(menu.Second, menu.Tree.SelectedItem);
        Assert.Same(menu.Second, shown);

        PressKey(menu, Keys.Up);

        Assert.Same(menu.First, menu.Tree.SelectedItem);
        Assert.Same(menu.First, shown);
    }

    [Fact]
    public void DoubleClickingADemo_ReachesItsOwnDoubleClickHandler()
    {
        // MGTreeView.ItemDoubleClicked fires only for items that have children, so the screen listens to the leaf.
        var menu = LoadMenuWithTwoDemos();
        int leafDoubleClicks = 0;
        int treeDoubleClicks = 0;
        menu.Second.MouseHandler.LMBDoubleClickedInside += (_, _) => leafDoubleClicks++;
        menu.Tree.ItemDoubleClicked += (_, _) => treeDoubleClicks++;

        var centre = CentreOf(menu.Second.HeaderContent);
        Click(menu, centre);
        Click(menu, centre);

        Assert.Equal(1, leafDoubleClicks);
        Assert.Equal(0, treeDoubleClicks);
        Assert.Same(menu.Second, menu.Tree.SelectedItem);
    }

    [Fact]
    public void EnterAndEscape_OnTheFocusedTree_LeaveTheSelectionAsItIs()
    {
        // Enter toggles the expansion of the selected item inside MGTreeView and raises nothing a screen could listen to:
        // the game reads Enter itself to launch. Escape is an MGUI cancel the tree does not handle: nothing quits.
        var menu = LoadMenuWithTwoDemos();
        SelectAndFocus(menu, menu.Second);
        int selectionChanges = 0;
        menu.Tree.SelectionChanged += (_, _) => selectionChanges++;

        PressKey(menu, Keys.Enter);
        PressKey(menu, Keys.Escape);

        Assert.Same(menu.Second, menu.Tree.SelectedItem);
        Assert.Equal(0, selectionChanges);
        Assert.Contains(menu.Window, menu.Desktop.Windows);
    }

    [Theory]
    [InlineData("btnLaunch")]
    [InlineData("btnQuit")]
    public void ClickingAButton_RunsItsCommand(string buttonName)
    {
        var menu = LoadMenuWithTwoDemos();
        Assert.True(menu.Window.TryGetElementByName(buttonName, out MGButton button));
        int commands = 0;
        button.AddCommandHandler((_, _) => commands++);

        Click(menu, CentreOf(button));

        Assert.Equal(1, commands);
    }
}
