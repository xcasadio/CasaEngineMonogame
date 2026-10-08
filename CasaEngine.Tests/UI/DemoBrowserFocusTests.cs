using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The keyboard rule of the demo browser (plan point P9 of `ai-agent/tasks/demo-browser-split-tasks.md`, ADR-0070), on
/// the real `demo-browser.xaml` and a headless desktop. MGUI focuses a focusable element by itself on a key press and
/// has no public way to drop the focus, so the browser keeps its focusable elements (the tree and the collapse button)
/// non-focusable while the pointer is elsewhere. These tests pin the MGUI behaviour that rule relies on, applied to the
/// same two elements <c>DemoBrowserScreen.SetKeyboardArmed</c> toggles (the screen itself lives in the demos app, which
/// this project does not reference).
/// </summary>
public class DemoBrowserFocusTests
{
    // The pointer stays outside the window: nothing is hovered, so only the keyboard can move the focus.
    private static readonly MouseState PointerAway = new(
        -100, -100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static (MGDesktop Desktop, HeadlessUiTestHarness.HeadlessRuntime Runtime, MGElement[] Focusables) LoadBrowser()
    {
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(280, 768);
        var window = UIScreenLoader.Load(desktop, XamlDocumentSource.FromFile(DemoScreenXamlTests.ScreenPath("demo-browser.xaml")));
        desktop.Windows.Add(window);

        Assert.True(window.TryGetElementByName("treeDemos", out MGTreeView tree));
        Assert.True(window.TryGetElementByName("btnCollapse", out MGButton collapse));
        return (desktop, runtime, new MGElement[] { tree, collapse });
    }

    private static void Frame(HeadlessUiTestHarness.HeadlessRuntime runtime, MGDesktop desktop, int totalElapsedMs, KeyboardState keyboard)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            PointerAway,
            keyboard));
        desktop.Update();
    }

    private static void SetArmed(MGElement[] focusables, bool armed)
    {
        foreach (var element in focusables)
        {
            element.IsFocusable = armed;
        }
    }

    [Fact]
    public void Armed_AKeyPress_LetsMguiFocusTheBrowserByItself()
    {
        // The threat the rule answers: an armed browser takes the keyboard on any key, wherever the pointer is.
        var (desktop, runtime, focusables) = LoadBrowser();
        SetArmed(focusables, true);

        Frame(runtime, desktop, 16, new KeyboardState());
        Frame(runtime, desktop, 32, new KeyboardState(Keys.Down));
        Frame(runtime, desktop, 48, new KeyboardState());

        Assert.NotNull(desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void Disarmed_KeyPresses_LeaveNothingFocused()
    {
        // A demo key pressed before the player ever touched the browser: the browser must not take the keyboard.
        var (desktop, runtime, focusables) = LoadBrowser();
        SetArmed(focusables, false);

        Frame(runtime, desktop, 16, new KeyboardState());
        Frame(runtime, desktop, 32, new KeyboardState(Keys.Down));
        Frame(runtime, desktop, 48, new KeyboardState(Keys.Space));
        Frame(runtime, desktop, 64, new KeyboardState());

        Assert.Null(desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void Disarming_AFocusedBrowser_DropsTheFocusAtTheNextUpdate()
    {
        // The pointer leaves the browser: the scene gets the keyboard back one update later.
        var (desktop, runtime, focusables) = LoadBrowser();
        SetArmed(focusables, true);
        Frame(runtime, desktop, 16, new KeyboardState());
        Frame(runtime, desktop, 32, new KeyboardState(Keys.Down));
        Frame(runtime, desktop, 48, new KeyboardState());
        Assert.NotNull(desktop.FocusedKeyboardHandler);

        SetArmed(focusables, false);
        Frame(runtime, desktop, 64, new KeyboardState(Keys.Down));
        Assert.Null(desktop.FocusedKeyboardHandler);

        Frame(runtime, desktop, 80, new KeyboardState());
        Assert.Null(desktop.FocusedKeyboardHandler);
    }
}
