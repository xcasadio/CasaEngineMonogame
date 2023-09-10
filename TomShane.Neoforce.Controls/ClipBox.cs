using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class ClipBox : Control
{
    public ClipBox(Manager manager) : base(manager)
    {
        Color = Color.Transparent;
        BackColor = Color.Transparent;
        CanFocus = false;
        Passive = true;
    }
}