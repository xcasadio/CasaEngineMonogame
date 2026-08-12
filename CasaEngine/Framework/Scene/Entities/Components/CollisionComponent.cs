using System.ComponentModel;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Collision volume of an entity: a list of <see cref="ColliderFixture"/> lowered to one physics body
/// per collision profile.
/// </summary>
[DisplayName("Collision")]
public class CollisionComponent : PhysicsBaseComponent
{
    public List<ColliderFixture> Fixtures { get; } = new();

    public CollisionComponent()
    {
    }

    public CollisionComponent(CollisionComponent other) : base(other)
    {
        for (int i = 0; i < other.Fixtures.Count; i++)
        {
            Fixtures.Add(new ColliderFixture(other.Fixtures[i]));
        }
    }

    public override CollisionComponent Clone()
    {
        return new CollisionComponent(this);
    }

    protected override IReadOnlyList<ColliderFixture> GetFixtures()
    {
        return Fixtures;
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        var boundingBox = new BoundingBox();

        for (int i = 0; i < Fixtures.Count; i++)
        {
            var fixture = Fixtures[i];
            if (fixture.Shape == null)
            {
                continue;
            }

            var fixtureBoundingBox = fixture.Shape.BoundingBox;
            if (!fixture.HasIdentityPose)
            {
                fixtureBoundingBox = fixtureBoundingBox.Transform(fixture.GetLocalMatrix());
            }

            boundingBox.ExpandBy(fixtureBoundingBox);
        }

        return boundingBox;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        Fixtures.Clear();

        if (element["fixtures"] is not JArray fixturesArray)
        {
            return;
        }

        foreach (var fixtureNode in fixturesArray)
        {
            var fixture = new ColliderFixture();
            fixture.Load((JObject)fixtureNode);
            Fixtures.Add(fixture);
        }
    }
}
