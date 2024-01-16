namespace TomShane.Neoforce.Controls.Skins;

public class SkinLayer : SkinBase
{

    public SkinImage Image = new();
    public int Width;
    public int Height;
    public int OffsetX;
    public int OffsetY;
    public Alignment Alignment;
    public Margins SizingMargins;
    public Margins ContentMargins;
    public SkinStates<LayerStates> States;
    public SkinStates<LayerOverlays> Overlays;
    public readonly SkinText Text = new();
    public readonly SkinList<SkinAttribute> Attributes = new();

    public SkinLayer()
        : base()
    {
        States.Enabled.Color = Color.White;
        States.Pressed.Color = Color.White;
        States.Focused.Color = Color.White;
        States.Hovered.Color = Color.White;
        States.Disabled.Color = Color.White;

        Overlays.Enabled.Color = Color.White;
        Overlays.Pressed.Color = Color.White;
        Overlays.Focused.Color = Color.White;
        Overlays.Hovered.Color = Color.White;
        Overlays.Disabled.Color = Color.White;
    }

    public SkinLayer(SkinLayer source)
        : base(source)
    {
        if (source != null)
        {
            Image = new SkinImage(source.Image);
            Width = source.Width;
            Height = source.Height;
            OffsetX = source.OffsetX;
            OffsetY = source.OffsetY;
            Alignment = source.Alignment;
            SizingMargins = source.SizingMargins;
            ContentMargins = source.ContentMargins;
            States = source.States;
            Overlays = source.Overlays;
            Text = new SkinText(source.Text);
            Attributes = new SkinList<SkinAttribute>(source.Attributes);
        }
        else
        {
            throw new Exception("Parameter for SkinLayer copy constructor cannot be null.");
        }
    }

}