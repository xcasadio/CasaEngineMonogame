﻿using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class StatusBar : Control
{
    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        Left = 0;
        Top = 0;
        Width = 64;
        Height = 24;
        CanFocus = false;
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["StatusBar"]);
    }
}