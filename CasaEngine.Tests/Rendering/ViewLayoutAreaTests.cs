using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The layout area of <see cref="ViewManager"/> (ADR-0070): back-buffer views laid out inside the screen minus
/// <see cref="ViewManager.LayoutInsets"/>, so a window-level UI can sit beside the scene. With no margin, every result
/// is the one the engine always produced.
/// </summary>
public class ViewLayoutAreaTests
{
    public static TheoryData<int, SplitMode> CountsAndModes()
    {
        var data = new TheoryData<int, SplitMode>();
        for (int count = 1; count <= 4; count++)
        {
            data.Add(count, SplitMode.Vertical);
            data.Add(count, SplitMode.Horizontal);
            data.Add(count, SplitMode.Grid4);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CountsAndModes))]
    public void Compute_AnAreaAtTheOrigin_GivesTheFullScreenRectangles(int viewCount, SplitMode mode)
    {
        var fullScreen = SplitScreenLayout.Compute(1023, 767, viewCount, mode);
        var area = SplitScreenLayout.Compute(new Rectangle(0, 0, 1023, 767), viewCount, mode);

        Assert.Equal(fullScreen, area);
    }

    [Theory]
    [MemberData(nameof(CountsAndModes))]
    public void Compute_AnOffsetArea_ShiftsEachRectangleByTheAreaOrigin(int viewCount, SplitMode mode)
    {
        var atOrigin = SplitScreenLayout.Compute(new Rectangle(0, 0, 744, 768), viewCount, mode);
        var offset = SplitScreenLayout.Compute(new Rectangle(280, 10, 744, 768), viewCount, mode);

        Assert.Equal(atOrigin.Length, offset.Length);
        for (int i = 0; i < atOrigin.Length; i++)
        {
            var expected = atOrigin[i];
            expected.Offset(280, 10);
            Assert.Equal(expected, offset[i]);
        }
    }

    [Fact]
    public void Compute_TwoVerticalViewsInTheSceneArea_SplitTheAreaNotTheScreen()
    {
        var rects = SplitScreenLayout.Compute(new Rectangle(280, 0, 744, 768), 2, SplitMode.Vertical);

        Assert.Equal(new Rectangle(280, 0, 372, 768), rects[0]);
        Assert.Equal(new Rectangle(652, 0, 372, 768), rects[1]);
    }

    [Fact]
    public void GetLayoutArea_WithoutMargins_IsTheWholeScreen()
    {
        var viewManager = new ViewManager();

        Assert.Equal(ViewLayoutInsets.Zero, viewManager.LayoutInsets);
        Assert.Equal(new Rectangle(0, 0, 1024, 768), viewManager.GetLayoutArea(1024, 768));
    }

    [Fact]
    public void GetLayoutArea_WithMargins_IsTheScreenMinusTheMargins()
    {
        var viewManager = new ViewManager { LayoutInsets = new ViewLayoutInsets(280, 4, 10, 20) };

        Assert.Equal(new Rectangle(280, 4, 1024 - 280 - 10, 768 - 4 - 20), viewManager.GetLayoutArea(1024, 768));
    }

    [Fact]
    public void GetLayoutArea_MarginsWiderThanTheScreen_KeepOnePixelInsideTheScreen()
    {
        var viewManager = new ViewManager { LayoutInsets = new ViewLayoutInsets(5000, 5000, 5000, 5000) };

        Assert.Equal(new Rectangle(1023, 767, 1, 1), viewManager.GetLayoutArea(1024, 768));
    }

    [Fact]
    public void GetLayoutArea_NegativeMargins_CountAsZero()
    {
        var viewManager = new ViewManager { LayoutInsets = new ViewLayoutInsets(-10, -10, -10, -10) };

        Assert.Equal(new Rectangle(0, 0, 1024, 768), viewManager.GetLayoutArea(1024, 768));
    }

    [Fact]
    public void ApplyBackBufferLayout_WithALeftMargin_PlacesTheViewsAndSizesTheirCamerasInsideTheArea()
    {
        var viewManager = new ViewManager
        {
            AutoLayoutMode = SplitMode.Vertical,
            LayoutInsets = new ViewLayoutInsets(280, 0, 0, 0),
        };
        var world = new World();
        var leftCamera = new Camera2dComponent();
        var rightCamera = new Camera2dComponent();
        var leftSurface = new BackBufferSurface(new Rectangle(0, 0, 512, 768));
        var rightSurface = new BackBufferSurface(new Rectangle(512, 0, 512, 768));
        viewManager.Add(new RenderView(world, leftCamera, leftSurface));
        viewManager.Add(new RenderView(world, rightCamera, rightSurface));

        viewManager.ApplyBackBufferLayout(1024, 768);

        Assert.Equal(new Rectangle(280, 0, 372, 768), leftSurface.ViewportRect);
        Assert.Equal(new Rectangle(652, 0, 372, 768), rightSurface.ViewportRect);
        Assert.Equal(372, leftCamera.Viewport.Width);
        Assert.Equal(768, leftCamera.Viewport.Height);
        Assert.Equal(372, rightCamera.Viewport.Width);
    }

    [Fact]
    public void ApplyBackBufferLayout_WithoutMargins_CoversTheWholeScreenAsBefore()
    {
        var viewManager = new ViewManager { AutoLayoutMode = SplitMode.Vertical };
        var world = new World();
        var leftSurface = new BackBufferSurface(new Rectangle(0, 0, 10, 10));
        var rightSurface = new BackBufferSurface(new Rectangle(0, 0, 10, 10));
        viewManager.Add(new RenderView(world, new Camera2dComponent(), leftSurface));
        viewManager.Add(new RenderView(world, new Camera2dComponent(), rightSurface));

        viewManager.ApplyBackBufferLayout(1024, 768);

        Assert.Equal(new Rectangle(0, 0, 512, 768), leftSurface.ViewportRect);
        Assert.Equal(new Rectangle(512, 0, 512, 768), rightSurface.ViewportRect);
    }
}
