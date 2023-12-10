using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public enum RadioButtonMode
{
    Auto,
    Manual
}

public class RadioButton : CheckBox
{

    private const string SkRadioButton = "RadioButton";

    public RadioButtonMode Mode { get; set; } = RadioButtonMode.Auto;

    public RadioButton(Manager manager) : base(manager)
    {
    }

    public override void Init()
    {
        base.Init();
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkRadioButton]);
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);
    }

    protected override void OnClick(EventArgs e)
    {
        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            if (Mode == RadioButtonMode.Auto)
            {
                if (Parent != null)
                {
                    var lst = Parent.Controls as ControlsList;
                    for (var i = 0; i < lst.Count; i++)
                    {
                        if (lst[i] is RadioButton)
                        {
                            (lst[i] as RadioButton).Checked = false;
                        }
                    }
                }
                else if (Parent == null && Manager != null)
                {
                    var lst = Manager.Controls as ControlsList;

                    for (var i = 0; i < lst.Count; i++)
                    {
                        if (lst[i] is RadioButton)
                        {
                            (lst[i] as RadioButton).Checked = false;
                        }
                    }
                }
            }
        }
        base.OnClick(e);
    }

}