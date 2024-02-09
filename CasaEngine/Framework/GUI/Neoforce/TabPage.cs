using FontStashSharp;

namespace TomShane.Neoforce.Controls;

public class TabPage : Control
{
    protected internal Rectangle HeaderRect { get; private set; } = Rectangle.Empty;

    public override void Initialize(Manager manager)
    {
        Color = Color.Transparent;

        base.Initialize(manager);

        Passive = true;
        CanFocus = false;
    }

    protected internal void CalcRect(Rectangle prev, SpriteFontBase font, Margins margins, Point offset, bool first)
    {
        var size = (int)Math.Ceiling(font.MeasureString(Text).X) + margins.Horizontal;

        if (first)
        {
            offset.X = 0;
        }

        HeaderRect = new Rectangle(prev.Right + offset.X, prev.Top, size, prev.Height);
    }
}