using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors;

public class WallAvoidance : SteeringBehavior
{

    public WallAvoidance(string name, MovingObject owner, float modifier)
        : base(name, owner, modifier)
    { }

    public override Vector3 Calculate()
    {
        var physicsEngineComponent = EngineComponents.Game.GetGameComponent<PhysicsEngineComponent>();

        if (physicsEngineComponent.PhysicsEngine == null)
        {
            throw new NullReferenceException("MovingObject.CanMoveBetween() : PhysicEngine.Physic not defined");
        }

        var feelers = new Vector3[3];
        var scale = 2.0f + owner.Speed * 0.5f;
        var force = Vector3.Zero;

        //Create the feelers
        feelers[0] = owner.Position + owner.Look * scale;
        feelers[1] = owner.Position + Vector3.TransformNormal(owner.Look, Matrix.CreateRotationY(MathHelper.ToRadians(-40.0f))) * scale * 0.5f;
        feelers[2] = owner.Position + Vector3.TransformNormal(owner.Look, Matrix.CreateRotationY(MathHelper.ToRadians(40.0f))) * scale * 0.5f;

        var nearIntersectionDist = float.MaxValue;

        for (var i = 0; i < feelers.Length; i++)
        {
            var position = owner.Position;

            //Test for a collision
            owner.Position = position;

            //If there was a collision see the collision distance
            if (physicsEngineComponent.PhysicsEngine.NearBodyWorldRayCast(ref position, ref feelers[i], out var contactPoint, out var contactNormal))
            {
                var intersectionDist = (contactPoint - owner.Position).Length();

                //If it was closer than the the closer collision so far, update the values
                if (intersectionDist < nearIntersectionDist)
                {
                    nearIntersectionDist = intersectionDist;
                    var overShoot = contactPoint - feelers[i];
                    force = contactNormal * overShoot.Length() * scale;
                }
            }
        }

        return force;
    }

}