using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public readonly record struct CharacterControllerInputSnapshot(
    Vector2 MoveIntent,
    bool JumpRequested,
    bool DashRequested,
    Vector2 DashDirection)
{
    public JObject Save()
    {
        return new JObject
        {
            ["move_intent"] = SaveVector2(MoveIntent),
            ["jump_requested"] = JumpRequested,
            ["dash_requested"] = DashRequested,
            ["dash_direction"] = SaveVector2(DashDirection),
        };
    }

    public static CharacterControllerInputSnapshot Load(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return new CharacterControllerInputSnapshot(
            element["move_intent"]?.GetVector2() ?? Vector2.Zero,
            element["jump_requested"]?.Value<bool>() ?? false,
            element["dash_requested"]?.Value<bool>() ?? false,
            element["dash_direction"]?.GetVector2() ?? Vector2.Zero);
    }

    private static JObject SaveVector2(Vector2 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
        };
    }
}