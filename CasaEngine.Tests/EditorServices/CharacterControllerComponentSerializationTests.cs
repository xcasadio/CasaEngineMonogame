using System.Linq;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// E3.d.0: <see cref="CharacterControllerComponent"/> used to fall into
/// <c>EditorEntityJsonSerializer</c>'s generic <c>default:</c> save branch, which only writes
/// <c>ObjectBase</c> + <c>type</c> - its settings and control mode were silently dropped on save even
/// though <see cref="CharacterControllerComponent.Load"/> reads both back
/// (docs/plan-e3-collisions.md, E3.d.0).
/// </summary>
public class CharacterControllerComponentSerializationTests
{
    [Fact]
    public void SaveEntity_ThenLoad_RoundTripsEverySettingAndTheControlMode()
    {
        var entity = new Entity
        {
            RootComponent = new TransformComponent(),
        };

        var component = new CharacterControllerComponent();
        component.Settings.Radius = 7.5f;
        component.Settings.Height = 32f;
        component.Settings.SkinWidth = 0.5f;
        component.Settings.MaxHorizontalSpeed = 12.5f;
        component.Settings.Acceleration = 45f;
        component.Settings.Deceleration = 55f;
        component.Settings.Gravity = 1250f;
        component.Settings.JumpSpeed = 6.5f;
        component.Settings.CoyoteTimeSeconds = 0.08f;
        component.Settings.JumpBufferSeconds = 0.12f;
        component.Settings.DashSpeed = 20f;
        component.Settings.DashDurationSeconds = 0.2f;
        component.Settings.DashCooldownSeconds = 0.9f;
        component.Settings.MaxSlopeAngle = 38f;
        component.Settings.GroundSnapDistance = 4f;
        component.Settings.StepHeight = 3f;
        component.Settings.ProfileName = "CustomProfile";
        component.Settings.HitTriggers = true;
        component.Settings.WalkabilityMask = 0x41u;
        component.Settings.MaxFallSpeed = 800f;
        component.SetControlMode(CharacterControlMode.AI);

        entity.AddComponent(component);

        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(entity, node);

        var loaded = new Entity();
        loaded.Load(node);

        var loadedComponent = Assert.Single(loaded.Components.OfType<CharacterControllerComponent>());
        var loadedSettings = loadedComponent.Settings;

        Assert.Equal(7.5f, loadedSettings.Radius);
        Assert.Equal(32f, loadedSettings.Height);
        Assert.Equal(0.5f, loadedSettings.SkinWidth);
        Assert.Equal(12.5f, loadedSettings.MaxHorizontalSpeed);
        Assert.Equal(45f, loadedSettings.Acceleration);
        Assert.Equal(55f, loadedSettings.Deceleration);
        Assert.Equal(1250f, loadedSettings.Gravity);
        Assert.Equal(6.5f, loadedSettings.JumpSpeed);
        Assert.Equal(0.08f, loadedSettings.CoyoteTimeSeconds);
        Assert.Equal(0.12f, loadedSettings.JumpBufferSeconds);
        Assert.Equal(20f, loadedSettings.DashSpeed);
        Assert.Equal(0.2f, loadedSettings.DashDurationSeconds);
        Assert.Equal(0.9f, loadedSettings.DashCooldownSeconds);
        Assert.Equal(38f, loadedSettings.MaxSlopeAngle);
        Assert.Equal(4f, loadedSettings.GroundSnapDistance);
        Assert.Equal(3f, loadedSettings.StepHeight);
        Assert.Equal("CustomProfile", loadedSettings.ProfileName);
        Assert.True(loadedSettings.HitTriggers);
        Assert.Equal(0x41u, loadedSettings.WalkabilityMask);
        Assert.Equal(800f, loadedSettings.MaxFallSpeed);
        Assert.Equal(CharacterControlMode.AI, loadedComponent.ControlMode);
    }
}
