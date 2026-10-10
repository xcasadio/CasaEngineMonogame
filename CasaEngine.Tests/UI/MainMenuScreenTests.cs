using CasaEngine.Demos.Demos;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The demos main screen itself (<c>MainMenuScreen</c>, linked from the demos app by this project), built on a headless
/// desktop from the shipped `main-menu.xaml`: the tree follows the theme order and the game's list, the initial
/// selection and its sheet, the sheet of a theme, the Launch and Quit buttons, the double click on a demo, the loading
/// state, and a theme missing from the order. <see cref="DemoMainMenuTests"/> pins the MGUI behaviours the screen relies
/// on (arrows, Enter, Escape).
/// </summary>
public class MainMenuScreenTests
{
    private static readonly Point PointerAway = new(-100, -100);

    private static readonly MainMenuScreen.Entry[] Entries =
    {
        new("Cutscene A", "First cutscene.", "Cutscenes"),
        new("Box physics", "Boxes falling.", "Physics"),
        new("Cutscene B", "Second cutscene.", "Cutscenes"),
        new("Model", "A static model.", "Rendering"),
    };

    // "Animation" has no demo: the tree leaves it out.
    private static readonly string[] ThemeOrder = { "Rendering", "Animation", "Physics", "Cutscenes" };

    private sealed class Menu
    {
        public MGDesktop Desktop;
        public HeadlessUiTestHarness.HeadlessRuntime Runtime;
        public MainMenuScreen Screen;
        public MGWindow Window;
        public MGTreeView Tree;
        public readonly List<int> Launches = new();
        public int Quits;
        public int Frame;

        public T Control<T>(string name) where T : MGElement
        {
            Assert.True(Window.TryGetElementByName(name, out T element), $"main-menu.xaml has no {typeof(T).Name} '{name}'");
            return element;
        }
    }

    private static Menu Open(int initialDemoIndex, MainMenuScreen.Entry[] entries = null)
    {
        var menu = new Menu();
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        menu.Desktop = desktop;
        menu.Runtime = runtime;
        menu.Screen = new MainMenuScreen(entries ?? Entries, ThemeOrder, initialDemoIndex, menu.Launches.Add, () => menu.Quits++);
        menu.Window = menu.Screen.BuildWindow(desktop);
        desktop.Windows.Add(menu.Window);
        menu.Tree = menu.Control<MGTreeView>("treeDemos");

        Step(menu, PointerAway, ButtonState.Released);
        Step(menu, PointerAway, ButtonState.Released);
        return menu;
    }

    private static void Step(Menu menu, Point pointer, ButtonState left)
    {
        menu.Frame++;
        menu.Runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(16 * menu.Frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(pointer.X, pointer.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            new KeyboardState()));
        menu.Desktop.Update();
    }

    private static void Click(Menu menu, MGElement element)
    {
        var point = element.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, element.LayoutBounds.Center);
        Step(menu, point, ButtonState.Released);
        Step(menu, point, ButtonState.Pressed);
        Step(menu, point, ButtonState.Released);
    }

    private static string HeaderOf(MGTreeViewItem item) => ((MGTextBlock)item.Header).Text;

    private static MGTreeViewItem ThemeItem(Menu menu, string theme)
        => Assert.Single(menu.Tree.Items, item => HeaderOf(item) == theme);

    private static MGTreeViewItem DemoItem(Menu menu, string theme, string title)
        => Assert.Single(ThemeItem(menu, theme).Items, item => HeaderOf(item) == title);

    [Fact]
    public void TheTree_ListsTheThemesInTheirOrder_EachWithItsDemosInTheGameOrder()
    {
        var menu = Open(0);

        Assert.Equal(new[] { "Rendering", "Physics", "Cutscenes" }, menu.Tree.Items.Select(HeaderOf));
        Assert.Equal(new[] { "Model" }, ThemeItem(menu, "Rendering").Items.Select(HeaderOf));
        Assert.Equal(new[] { "Box physics" }, ThemeItem(menu, "Physics").Items.Select(HeaderOf));
        Assert.Equal(new[] { "Cutscene A", "Cutscene B" }, ThemeItem(menu, "Cutscenes").Items.Select(HeaderOf));
        Assert.Equal("CasaEngine demos (4)", menu.Control<MGTextBlock>("lblHeader").Text);
    }

    [Fact]
    public void TheInitialDemo_IsSelectedUnderItsExpandedTheme_AndTheSheetShowsIt()
    {
        var menu = Open(2);

        Assert.Same(DemoItem(menu, "Cutscenes", "Cutscene B"), menu.Tree.SelectedItem);
        Assert.True(ThemeItem(menu, "Cutscenes").IsExpanded);
        Assert.True(menu.Screen.TryGetSelectedDemo(out int selected));
        Assert.Equal(2, selected);
        Assert.Equal("Cutscene B", menu.Control<MGTextBlock>("lblTitle").Text);
        Assert.Equal("Cutscenes", menu.Control<MGTextBlock>("lblTheme").Text);
        Assert.Equal("Second cutscene.", menu.Control<MGTextBlock>("lblDescription").Text);
        Assert.True(menu.Control<MGButton>("btnLaunch").IsEnabled);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void AnInitialIndexOutOfRange_SelectsTheFirstDemoOfTheGameList(int initialDemoIndex)
    {
        var menu = Open(initialDemoIndex);

        Assert.True(menu.Screen.TryGetSelectedDemo(out int selected));
        Assert.Equal(0, selected);
        Assert.Same(DemoItem(menu, "Cutscenes", "Cutscene A"), menu.Tree.SelectedItem);
    }

    [Fact]
    public void SelectingATheme_ShowsItsDemos_AndDisablesLaunch()
    {
        var menu = Open(0);

        menu.Tree.SelectItem(ThemeItem(menu, "Cutscenes"));

        Assert.False(menu.Screen.TryGetSelectedDemo(out _));
        Assert.Equal("Cutscenes", menu.Control<MGTextBlock>("lblTitle").Text);
        Assert.Equal("2 demos", menu.Control<MGTextBlock>("lblTheme").Text);
        Assert.Equal("Cutscene A\nCutscene B", menu.Control<MGTextBlock>("lblDescription").Text);
        Assert.False(menu.Control<MGButton>("btnLaunch").IsEnabled);
    }

    [Fact]
    public void TheLaunchButton_LaunchesTheSelectedDemo()
    {
        var menu = Open(3);

        Click(menu, menu.Control<MGButton>("btnLaunch"));

        Assert.Equal(new[] { 3 }, menu.Launches);
        Assert.Equal(0, menu.Quits);
    }

    [Fact]
    public void TheQuitButton_AsksToQuit_AndLaunchesNothing()
    {
        var menu = Open(3);

        Click(menu, menu.Control<MGButton>("btnQuit"));

        Assert.Equal(1, menu.Quits);
        Assert.Empty(menu.Launches);
    }

    [Fact]
    public void DoubleClickingADemo_LaunchesThatDemo()
    {
        var menu = Open(1);
        var leaf = DemoItem(menu, "Physics", "Box physics");

        Click(menu, leaf.HeaderContent);
        Click(menu, leaf.HeaderContent);

        Assert.Equal(new[] { 1 }, menu.Launches);
    }

    [Fact]
    public void ShowLoading_NamesTheDemo_AndDisablesBothButtons()
    {
        var menu = Open(1);

        menu.Screen.ShowLoading(1);

        Assert.Equal("Loading Box physics...", menu.Control<MGTextBlock>("lblStatus").Text);
        Assert.False(menu.Control<MGButton>("btnLaunch").IsEnabled);
        Assert.False(menu.Control<MGButton>("btnQuit").IsEnabled);
    }

    [Fact]
    public void ADemoWhoseThemeIsNotInTheOrder_FailsToBuild_NamingIt()
    {
        var entries = new MainMenuScreen.Entry[] { new("Voices", "Audio voices.", "Audio") };
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        var screen = new MainMenuScreen(entries, ThemeOrder, 0, _ => { }, () => { });

        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(desktop));

        Assert.Contains("'Voices'", error.Message, StringComparison.Ordinal);
        Assert.Contains("'Audio'", error.Message, StringComparison.Ordinal);
    }
}
