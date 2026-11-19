using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>The editor cameras get an infinite far plane, again after an initialization reset it.</summary>
public sealed class EditorCameraFactoryTests
{
    [Fact]
    public void ApplySettings_GivesTheCameraAnInfiniteFarPlane()
    {
        var camera = new PerspectiveCameraComponent();
        camera.OnScreenResized(1920, 1080);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;

        EditorCameraFactory.ApplySettings(camera);

        Assert.Equal(float.PositiveInfinity, camera.FarPlane);
        Assert.Equal(1f, camera.NearPlane);
        Assert.Equal(-1f, camera.ProjectionMatrix.M33);
        Assert.Equal(-1f, camera.ProjectionMatrix.M43);
    }

    [Fact]
    public void ApplySettings_AfterTheDepthRangeIsReset_RestoresTheInfiniteFarPlane()
    {
        var camera = new PerspectiveCameraComponent();
        camera.OnScreenResized(640, 360);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        EditorCameraFactory.ApplySettings(camera);

        // CameraComponent.InitializeWithWorld puts the depth range back to 1..1000.
        camera.FarPlane = 1000f;
        EditorCameraFactory.ApplySettings(camera);

        Assert.Equal(EditorCameraFactory.FarPlane, camera.FarPlane);
        Assert.Equal(-1f, camera.ProjectionMatrix.M33);
    }
}
