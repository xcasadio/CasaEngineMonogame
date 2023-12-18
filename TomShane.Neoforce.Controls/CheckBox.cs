using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class CheckBox : ButtonBase
{

    private const string SkCheckBox = "CheckBox";
    private const string LrCheckBox = "Control";
    private const string LrChecked = "Checked";

    private bool _state;

    public virtual bool Checked
    {
        get => _state;
        set
        {
            _state = value;
            Invalidate();
            if (!Suspended)
            {
                OnCheckedChanged(new EventArgs());
            }
        }
    }

    public event EventHandler CheckedChanged;

    public CheckBox()
    {
        Width = 64;
        Height = 16;
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);
        CheckLayer(Skin, LrChecked);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkCheckBox]);

    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        var layer = Skin.Layers[LrChecked];
        var font = Skin.Layers[LrChecked].Text;

        if (!_state)
        {
            layer = Skin.Layers[LrCheckBox];
            font = Skin.Layers[LrCheckBox].Text;
        }

        rect.Width = layer.Width;
        rect.Height = layer.Height;
        var rc = new Rectangle(rect.Left + rect.Width + 4, rect.Y, Width - (layer.Width + 4), rect.Height);

        renderer.DrawLayer(this, layer, rect);
        renderer.DrawString(this, layer, Text, rc, false, 0, 0);
    }

    protected override void OnClick(EventArgs e)
    {
        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            Checked = !Checked;
        }
        base.OnClick(e);
    }

    protected virtual void OnCheckedChanged(EventArgs e)
    {
        CheckedChanged?.Invoke(this, e);
    }

}