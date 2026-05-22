using BulletSharp;
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

        Assert.Equal(CollisionFilterGroups.DefaultFilter, settings.CollisionGroup);
        Assert.Equal(CollisionFilterGroups.AllFilter, settings.CollisionMask);
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
            ["collision_mask"] = nameof(CollisionFilterGroups.StaticFilter),
            ["hit_triggers"] = true,
        };

        settings.Load(json);

        Assert.Equal(0.4f, settings.Radius);
        Assert.Equal(2.0f, settings.Height);
        Assert.Equal(7.0f, settings.MaxHorizontalSpeed);
        Assert.Equal(30f, settings.Acceleration);
        Assert.Equal(CollisionFilterGroups.StaticFilter, settings.CollisionMask);
        Assert.True(settings.HitTriggers);
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
    public void GroundInfo_NormalizesNonZeroNormal()
    {
        var groundInfo = new CharacterControllerGroundInfo(true, new Vector3(0f, 4f, 0f), null, 0f);

        Assert.Equal(Vector3.Up, groundInfo.Normal);
    }
}