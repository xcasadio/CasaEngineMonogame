namespace CasaEngine.Framework.GUI.Neoforce.Skins;

public class SkinText : SkinBase
{

    public SkinFont Font;
    public int OffsetX;
    public int OffsetY;
    public Alignment Alignment;
    public SkinStates<Color> Colors;

    public SkinText()
        : base()
    {
        Colors.Enabled = Color.White;
        Colors.Pressed = Color.White;
        Colors.Focused = Color.White;
        Colors.Hovered = Color.White;
        Colors.Disabled = Color.White;
    }

    public SkinText(SkinText source)
        : base(source)
    {
        if (source != null)
        {
            Font = new SkinFont(source.Font);
            OffsetX = source.OffsetX;
            OffsetY = source.OffsetY;
            Alignment = source.Alignment;
            Colors = source.Colors;
        }
    }

}