namespace TomShane.Neoforce.Controls.Skins;

public class SkinControl : SkinBase
{
    public string Inherits;
    public Size DefaultSize;
    public int ResizerSize;
    public Size MinimumSize;
    public Margins OriginMargins;
    public Margins ClientMargins;
    public readonly SkinList<SkinLayer> Layers = new();
    public readonly SkinList<SkinAttribute> Attributes = new();

    public SkinControl()
    {
    }

    public SkinControl(SkinControl source)
        : base(source)
    {
        if (source != null)
        {
            Inherits = source.Inherits;
            DefaultSize = source.DefaultSize;
            MinimumSize = source.MinimumSize;
            OriginMargins = source.OriginMargins;
            ClientMargins = source.ClientMargins;
            ResizerSize = source.ResizerSize;
            Layers = new SkinList<SkinLayer>(source.Layers);
            Attributes = new SkinList<SkinAttribute>(source.Attributes);
        }
    }
}