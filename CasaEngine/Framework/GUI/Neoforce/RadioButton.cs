using CasaEngine.Framework.GUI.Neoforce.Input;
using CasaEngine.Framework.GUI.Neoforce.Skins;

namespace CasaEngine.Framework.GUI.Neoforce;

public class RadioButton : CheckBox
{
    private const string SkRadioButton = "RadioButton";

    public RadioButtonMode Mode { get; set; } = RadioButtonMode.Auto;

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkRadioButton]);
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