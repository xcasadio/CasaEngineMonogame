using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CasaEngine.Engine.Physics.Shapes;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Physics")]
public class PhysicsComponent : Component
#if EDITOR
    , INotifyPropertyChanged
#endif
{
    public static readonly int ComponentId = (int)ComponentIds.Physics;
    private Shape _shape;

    public Vector3 Velocity { get; set; }

    //a normalized vector pointing in the direction the entity is heading. 
    public Vector3 Heading { get; set; }

    //a vector perpendicular to the heading vector
    public Vector3 Side { get; set; }

    public float Mass { get; set; }

    //the maximum speed this entity may travel at.
    public float MaxSpeed { get; set; }

    //the maximum force this entity can produce to power itself 
    //(think rockets and thrust)
    public float MaxForce { get; set; }

    //the maximum rate (radians per second)this vehicle can rotate         
    public float MaxTurnRate { get; set; }

    public Shape Shape
    {
        get => _shape;
        set
        {
            _shape = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public PhysicsComponent(Entity entity) : base(entity, ComponentId)
    {
    }

    public override void Initialize()
    {
        var physicsEngineComponent = EngineComponents.Game.GetGameComponent<PhysicsEngineComponent>();
        //physicsEngineComponent.
    }

    public override void Update(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {

    }

#if EDITOR
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
#endif
}