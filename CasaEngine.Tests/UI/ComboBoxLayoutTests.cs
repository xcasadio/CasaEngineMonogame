using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.UI;

public class ComboBoxLayoutTests
{
    [Fact]
    public void GetEffectiveSizeConstraints_ClampsMinimumToAvailableMaximum()
    {
        var (minSize, maxSize) = MGWindow.GetEffectiveSizeConstraints(180, 100, 133, 360);

        Assert.Equal(133, minSize.Width);
        Assert.Equal(100, minSize.Height);
        Assert.Equal(133, maxSize.Width);
        Assert.Equal(360, maxSize.Height);
    }

    [Fact]
    public void GetFittedDropdownLeft_ShiftsDropdownLeftWhenOpeningNearViewportRightEdge()
    {
        Rectangle viewport = new(0, 0, 1920, 1080);

        int left = MGComboBox<string>.GetFittedDropdownLeft(1787, 1920, 180, viewport, 1.0f);

        Assert.Equal(1740, left);
    }
}