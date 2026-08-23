using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

public class CharacterControllerSettingsTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var settings = new CharacterControllerSettings();

        settings.Validate();

        Assert.Equal(CollisionProfileNames.Pawn, settings.ProfileName);
        Assert.Equal(
            GameSettings.PhysicsEngineSettings.CollisionProfiles.GetResolved(CollisionProfileNames.Pawn).BlockMask,
            settings.GetSweepChannelMask());
    }

    [Fact]
    public void Load_OverridesKnownValues_AndKeepsMissingDefaults()
    {
        var settings = new CharacterControllerSettings();
        var json = new JObject
        {
            ["radius"] = 0.4f,
            ["height"] = 2.0f,
            ["max_horizontal_speed"] = 7.0f,
            ["step_height"] = 0.25f,
            ["coyote_time_seconds"] = 0.12f,
            ["jump_buffer_seconds"] = 0.15f,
            ["dash_speed"] = 12f,
            ["dash_duration_seconds"] = 0.2f,
            ["dash_cooldown_seconds"] = 0.4f,
            ["collision_profile"] = CollisionProfileNames.WorldDynamic,
            ["hit_triggers"] = true,
            ["walkability_mask"] = 0x8041u,
            ["max_fall_speed"] = 800f,
        };

        settings.Load(json);

        Assert.Equal(0.4f, settings.Radius);
        Assert.Equal(2.0f, settings.Height);
        Assert.Equal(7.0f, settings.MaxHorizontalSpeed);
        Assert.Equal(0.25f, settings.StepHeight);
        Assert.Equal(0.12f, settings.CoyoteTimeSeconds);
        Assert.Equal(0.15f, settings.JumpBufferSeconds);
        Assert.Equal(12f, settings.DashSpeed);
        Assert.Equal(0.2f, settings.DashDurationSeconds);
        Assert.Equal(0.4f, settings.DashCooldownSeconds);
        Assert.Equal(30f, settings.Acceleration);
        Assert.Equal(CollisionProfileNames.WorldDynamic, settings.ProfileName);
        Assert.True(settings.HitTriggers);
        Assert.Equal(0x8041u, settings.WalkabilityMask);
        Assert.Equal(800f, settings.MaxFallSpeed);
    }

    [Fact]
    public void Defaults_WalkabilityMaskAndMaxFallSpeed_AreUnconstrained()
    {
        var settings = new CharacterControllerSettings();

        Assert.Equal(0u, settings.WalkabilityMask);
        Assert.Equal(0f, settings.MaxFallSpeed);
    }

    [Fact]
    public void Clone_RoundTrips_WalkabilityMaskAndMaxFallSpeed()
    {
        var settings = new CharacterControllerSettings
        {
            WalkabilityMask = 0x8041u,
            MaxFallSpeed = 800f,
        };

        var cloned = settings.Clone();

        Assert.Equal(settings.WalkabilityMask, cloned.WalkabilityMask);
        Assert.Equal(settings.MaxFallSpeed, cloned.MaxFallSpeed);
    }

    [Fact]
    public void Validate_RejectsNegativeMaxFallSpeed()
    {
        var settings = new CharacterControllerSettings
        {
            MaxFallSpeed = -1f,
        };

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_RejectsInvalidCapsuleDimensions()
    {
        var settings = new CharacterControllerSettings
        {
            Radius = 1f,
            Height = 1.5f,
        };

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_RejectsInvalidSlopeAngle()
    {
        var settings = new CharacterControllerSettings
        {
            MaxSlopeAngle = 90f,
        };

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_RejectsNegativeStepHeight()
    {
        var settings = new CharacterControllerSettings
        {
            StepHeight = -0.01f,
        };

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_RejectsNegativeGameplayOptionValues()
    {
        Assert.Throws<InvalidOperationException>(() => new CharacterControllerSettings { CoyoteTimeSeconds = -0.01f }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CharacterControllerSettings { JumpBufferSeconds = -0.01f }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CharacterControllerSettings { DashSpeed = -0.01f }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CharacterControllerSettings { DashDurationSeconds = -0.01f }.Validate());
        Assert.Throws<InvalidOperationException>(() => new CharacterControllerSettings { DashCooldownSeconds = -0.01f }.Validate());
    }

    [Fact]
    public void GroundInfo_NormalizesNonZeroNormal()
    {
        var groundInfo = new CharacterControllerGroundInfo(true, new Vector3(0f, 4f, 0f), null, 0f);

        Assert.Equal(Vector3.Up, groundInfo.Normal);
    }
}