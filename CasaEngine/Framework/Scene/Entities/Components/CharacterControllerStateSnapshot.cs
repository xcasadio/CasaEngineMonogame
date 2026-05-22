using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public readonly record struct CharacterControllerStateSnapshot(
    Vector3 Position,
    Quaternion Orientation,
    CharacterControlMode ControlMode,
    CharacterMovementState MovementState,
    Vector3 Velocity,
    Vector2 MoveIntent,
    bool JumpRequested,
    float JumpBufferRemainingSeconds,
    float CoyoteTimeRemainingSeconds,
    bool DashRequested,
    Vector2 DashRequestedDirection,
    Vector2 DashDirection,
    float DashRemainingSeconds,
    float DashCooldownRemainingSeconds,
    bool IsGrounded,
    Vector3 GroundNormal,
    float GroundSlopeAngle,
    Vector3 LastRequestedDisplacement,
    Vector3 LastActualDisplacement)
{
    public JObject Save()
    {
        return new JObject
        {
            ["position"] = SaveVector3(Position),
            ["orientation"] = SaveQuaternion(Orientation),
            ["control_mode"] = ControlMode.ToString(),
            ["movement_state"] = MovementState.ToString(),
            ["velocity"] = SaveVector3(Velocity),
            ["move_intent"] = SaveVector2(MoveIntent),
            ["jump_requested"] = JumpRequested,
            ["jump_buffer_remaining_seconds"] = JumpBufferRemainingSeconds,
            ["coyote_time_remaining_seconds"] = CoyoteTimeRemainingSeconds,
            ["dash_requested"] = DashRequested,
            ["dash_requested_direction"] = SaveVector2(DashRequestedDirection),
            ["dash_direction"] = SaveVector2(DashDirection),
            ["dash_remaining_seconds"] = DashRemainingSeconds,
            ["dash_cooldown_remaining_seconds"] = DashCooldownRemainingSeconds,
            ["is_grounded"] = IsGrounded,
            ["ground_normal"] = SaveVector3(GroundNormal),
            ["ground_slope_angle"] = GroundSlopeAngle,
            ["last_requested_displacement"] = SaveVector3(LastRequestedDisplacement),
            ["last_actual_displacement"] = SaveVector3(LastActualDisplacement),
        };
    }

    public static CharacterControllerStateSnapshot Load(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return new CharacterControllerStateSnapshot(
            element["position"]?.GetVector3() ?? Vector3.Zero,
            element["orientation"]?.GetQuaternion() ?? Quaternion.Identity,
            ReadEnum(element, "control_mode", CharacterControlMode.Player),
            ReadEnum(element, "movement_state", CharacterMovementState.Falling),
            element["velocity"]?.GetVector3() ?? Vector3.Zero,
            element["move_intent"]?.GetVector2() ?? Vector2.Zero,
            element["jump_requested"]?.Value<bool>() ?? false,
            ReadSingle(element, "jump_buffer_remaining_seconds"),
            ReadSingle(element, "coyote_time_remaining_seconds"),
            element["dash_requested"]?.Value<bool>() ?? false,
            element["dash_requested_direction"]?.GetVector2() ?? Vector2.Zero,
            element["dash_direction"]?.GetVector2() ?? Vector2.Zero,
            ReadSingle(element, "dash_remaining_seconds"),
            ReadSingle(element, "dash_cooldown_remaining_seconds"),
            element["is_grounded"]?.Value<bool>() ?? false,
            element["ground_normal"]?.GetVector3() ?? Vector3.Up,
            ReadSingle(element, "ground_slope_angle"),
            element["last_requested_displacement"]?.GetVector3() ?? Vector3.Zero,
            element["last_actual_displacement"]?.GetVector3() ?? Vector3.Zero);
    }

    private static T ReadEnum<T>(JObject element, string propertyName, T defaultValue) where T : struct
    {
        var token = element[propertyName];
        return token == null ? defaultValue : Enum.Parse<T>(token.Value<string>()!, ignoreCase: true);
    }

    private static float ReadSingle(JObject element, string propertyName)
    {
        return element[propertyName]?.Value<float>() ?? 0f;
    }

    private static JObject SaveVector2(Vector2 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
        };
    }

    private static JObject SaveVector3(Vector3 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };
    }

    private static JObject SaveQuaternion(Quaternion value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
            ["w"] = value.W,
        };
    }
}