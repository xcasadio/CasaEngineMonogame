using Microsoft.Xna.Framework;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ToolBar : Control
{
    private int _row;

    public virtual int Row
    {
        get => _row;
        set
        {
            _row = value;
            if (_row < 0)
            {
                _row = 0;
            }

            if (_row > 7)
            {
                _row = 7;
            }
        }
    }

    public virtual bool FullRow { get; set; }

    public ToolBar(Manager manager) : base(manager)
    {
        Left = 0;
        Top = 0;
        Width = 64;
        Height = 24;
        CanFocus = false;
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ToolBar"]);
    }
}