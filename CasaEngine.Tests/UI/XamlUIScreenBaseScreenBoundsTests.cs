using Rectangle = Microsoft.Xna.Framework.Rectangle;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The callback a XAML screen gets when the bounds of its desktop change (ADR-0048, E19.s S-R5): the view of a
/// virtual-resolution game changes size when the window does, and a screen that placed its window from the bounds at
/// load time has to place it again.
/// <para/>
/// Drives <see cref="XamlUIScreenBase.NotifyScreenBounds"/> directly, as <see cref="UIRoot"/> does each frame through
/// <see cref="ScreenStack.NotifyScreenBounds"/>; a <c>UIRoot</c> itself needs a graphics device.
/// </summary>
public class XamlUIScreenBaseScreenBoundsTests
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Name="pnlRoot" Orientation="Vertical">
            <TextBlock Name="lblTitle" Text="Hello" />
          </StackPanel>
        </Window>
        """;

    [Fact]
    public void NotifyScreenBounds_BeforeTheWindowIsBuilt_CallsNothing()
    {
        var screen = new BoundsScreen();

        screen.NotifyScreenBounds(new Rectangle(0, 0, 960, 720));

        Assert.Empty(screen.Changes);
    }

    [Fact]
    public void NotifyScreenBounds_WithTheBoundsTheWindowWasBuiltWith_CallsNothing()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(640, 480);
        var screen = new BoundsScreen();
        screen.BuildWindow(desktop);

        screen.NotifyScreenBounds(desktop.ValidScreenBounds);

        Assert.Empty(screen.Changes);
    }

    [Fact]
    public void NotifyScreenBounds_WithNewBounds_CallsOnceWithThemAndNotAgainForTheSame()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(640, 480);
        var screen = new BoundsScreen();
        screen.BuildWindow(desktop);

        screen.NotifyScreenBounds(new Rectangle(0, 0, 960, 720));
        screen.NotifyScreenBounds(new Rectangle(0, 0, 960, 720));

        Assert.Equal(new[] { new Rectangle(0, 0, 960, 720) }, screen.Changes);

        screen.NotifyScreenBounds(new Rectangle(0, 0, 640, 480));

        Assert.Equal(new[] { new Rectangle(0, 0, 960, 720), new Rectangle(0, 0, 640, 480) }, screen.Changes);
    }

    [Fact]
    public void ScreenStack_NotifiesEveryXamlScreen_EvenOneFrozenUnderAModal()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop(640, 480);
        var below = new BoundsScreen(desktop);
        var modal = new BoundsScreen(desktop) { Modal = true };
        var stack = new ScreenStack(null);
        stack.Push(below);
        stack.Push(modal);

        stack.NotifyScreenBounds(new Rectangle(0, 0, 960, 720));

        Assert.Equal(new[] { new Rectangle(0, 0, 960, 720) }, below.Changes);
        Assert.Equal(new[] { new Rectangle(0, 0, 960, 720) }, modal.Changes);
    }

    [Fact]
    public void ScreenStack_SkipsAScreenThatIsNotXaml()
    {
        var stack = new ScreenStack(null);
        stack.Push(new PlainScreen());

        stack.NotifyScreenBounds(new Rectangle(0, 0, 960, 720));
    }

    private sealed class BoundsScreen : XamlUIScreenBase
    {
        private readonly MGDesktop _desktop;

        public List<Rectangle> Changes { get; } = new();

        public bool Modal { get; init; }

        public override UILayer Layer => UILayer.HUD;

        public override bool IsModal => Modal;

        public BoundsScreen(MGDesktop desktop = null)
            : base(XamlDocumentSource.FromString(Markup, "bounds-screen.xaml"))
        {
            _desktop = desktop;
        }

        protected override void OnInitialize(UIRoot root) => BuildWindow(_desktop);

        // The stack registers windows with its UIRoot's desktop, which a test has not got.
        public override IEnumerable<MGWindow> GetWindows() => Enumerable.Empty<MGWindow>();

        protected override void OnWindowLoaded(MGWindow window)
        {
        }

        protected override void OnScreenBoundsChanged(Rectangle bounds) => Changes.Add(bounds);
    }

    private sealed class PlainScreen : UIScreenBase
    {
        public override UILayer Layer => UILayer.HUD;

        protected override void OnInitialize(UIRoot root)
        {
        }

        public override IEnumerable<MGWindow> GetWindows() => Enumerable.Empty<MGWindow>();
    }
}
