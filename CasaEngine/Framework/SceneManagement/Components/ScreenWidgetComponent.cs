using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement.Components;

public class ScreenWidgetComponent : SceneComponent
{
    public ScreenGui ScreenGui { get; set; }

    public ScreenWidgetComponent(ScreenWidgetComponent? other = null) : base(other)
    {

    }

    public override ScreenWidgetComponent Clone()
    {
        return new ScreenWidgetComponent(this);
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        foreach (var control in ScreenGui.Controls)
        {
            control.Initialize(World.Game.GameManager.UiManager);
        }
    }

    public override BoundingBox GetBoundingBox()
    {
        const float length = 0.5f;
        var min = Vector3.One * -length;
        var max = Vector3.One * length;

        return new BoundingBox(min, max);
    }
}