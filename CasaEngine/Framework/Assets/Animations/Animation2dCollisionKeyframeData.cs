using CasaEngine.Engine.Physics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

/// <summary>
/// Collision fixture set of an animation timeline. Step semantics: the set is active from
/// <see cref="TimeSeconds"/> until the next keyframe, and no set is active before the first keyframe.
/// </summary>
public sealed class Animation2dCollisionKeyframeData
{
    public float TimeSeconds { get; set; }

    public List<ColliderFixture> Fixtures { get; } = new();

    public static Animation2dCollisionKeyframeData Load(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var keyframe = new Animation2dCollisionKeyframeData
        {
            TimeSeconds = node["time_seconds"]?.Value<float>() ?? 0f,
        };

        if (node["fixtures"] is JArray fixturesNode)
        {
            foreach (var fixtureNode in fixturesNode)
            {
                if (fixtureNode is JObject fixtureObject)
                {
                    var fixture = new ColliderFixture();
                    fixture.Load(fixtureObject);
                    keyframe.Fixtures.Add(fixture);
                }
            }
        }

        return keyframe;
    }
}
