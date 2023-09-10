using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public enum GroupBoxType
{
    Normal,
    Flat
}

public class GroupBox : Container
{

    private GroupBoxType _type = GroupBoxType.Normal;

    public virtual GroupBoxType Type
    {
        get => _type;
        set { _type = value; Invalidate(); }
    }

    public GroupBox(Manager manager)
        : base(manager)
    {
        CheckLayer(Skin, "Control");
        CheckLayer(Skin, "Flat");

        CanFocus = false;
        Passive = true;
        Width = 64;
        Height = 64;
        BackColor = Color.Transparent;
    }

    public override void Init()
    {
        base.Init();
    }

    private void AdjustClientMargins()
    {
        var layer = _type == GroupBoxType.Normal ? Skin.Layers["Control"] : Skin.Layers["Flat"];
        var font = layer.Text != null && layer.Text.Font != null ? layer.Text.Font.Resource : null;
        var size = font.MeasureString(Text);
        var cm = ClientMargins;
        cm.Top = string.IsNullOrWhiteSpace(Text) ? ClientTop : (int)size.Y;
        ClientMargins = new Margins(cm.Left, cm.Top, cm.Right, cm.Bottom);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AdjustClientMargins();
    }

    protected internal override void OnSkinChanged(EventArgs e)
    {
        base.OnSkinChanged(e);
        AdjustClientMargins();
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        var layer = _type == GroupBoxType.Normal ? Skin.Layers["Control"] : Skin.Layers["Flat"];
        var font = layer.Text != null && layer.Text.Font != null ? layer.Text.Font.Resource : null;
        var col = layer.Text != null ? layer.Text.Colors.Enabled : Color.White;
        var offset = new Point(layer.Text.OffsetX, layer.Text.OffsetY);
        var size = font.MeasureString(Text);
        size.Y = font.LineHeight;
        var r = new Rectangle(rect.Left, rect.Top + (int)(size.Y / 2), rect.Width, rect.Height - (int)(size.Y / 2));

        renderer.DrawLayer(this, layer, r);

        if (font != null && Text != null && Text != "")
        {
            var bg = new Rectangle(r.Left + offset.X, r.Top - (int)(size.Y / 2) + offset.Y, (int)size.X + layer.ContentMargins.Horizontal, (int)size.Y);
            renderer.DrawLayer(Manager.Skin.Controls["Control"].Layers[0], bg, new Color(64, 64, 64), 0);
            renderer.DrawString(this, layer, Text, new Rectangle(r.Left, r.Top - (int)(size.Y / 2), (int)size.X, (int)size.Y), true, 0, 0, false);
        }
    }

}