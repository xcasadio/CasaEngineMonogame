using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The 3d camera keeps its vertical field of view whatever the shape of the viewport (ADR-0095).
/// </summary>
public sealed class Camera3dFieldOfViewTests
{
    private const float Tolerance = 1e-4f;

    // M22 of a perspective projection is 1 / tan(vertical field of view / 2).
    private static float VerticalFieldOfView(CameraComponent camera)
        => 2f * MathF.Atan(1f / camera.ProjectionMatrix.M22);

    private static ArcBallCameraComponent CreateCamera(int width, int height)
    {
        var camera = new ArcBallCameraComponent();
        camera.OnScreenResized(width, height);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        return camera;
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1024, 768)]
    [InlineData(768, 1024)]
    public void ANewCamera_SeesFortyFiveDegreesVertically_WhateverTheViewport(int width, int height)
    {
        var camera = CreateCamera(width, height);

        Assert.Equal(MathHelper.PiOver4, camera.FieldOfView, Tolerance);
        Assert.Equal(MathHelper.PiOver4, VerticalFieldOfView(camera), Tolerance);
    }

    [Fact]
    public void AResize_KeepsTheFieldOfViewSetOnTheCamera()
    {
        var camera = CreateCamera(1920, 1080);
        camera.FieldOfView = 1.1f;

        camera.OnScreenResized(1024, 768);

        Assert.Equal(1.1f, camera.FieldOfView, Tolerance);
        Assert.Equal(1.1f, VerticalFieldOfView(camera), Tolerance);
    }

    [Fact]
    public void AWiderViewport_ShowsMoreOnTheSides()
    {
        var narrow = CreateCamera(1024, 768);
        var wide = CreateCamera(1920, 1080);

        // M11 of a perspective projection is 1 / tan(horizontal field of view / 2): smaller means wider.
        Assert.True(wide.ProjectionMatrix.M11 < narrow.ProjectionMatrix.M11);
    }

    [Theory]
    [InlineData("CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity")]
    [InlineData("Projects/SampleProject/Entities/camera.entity")]
    public void TheSavedCameras_LoadWithFortyFiveDegrees(string relativePath)
    {
        var entity = JObject.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath)));
        var camera = new ArcBallCameraComponent();
        camera.Load((JObject)entity["root_component"]!);
        camera.OnScreenResized(1024, 768);
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;

        Assert.Equal(MathHelper.PiOver4, camera.FieldOfView, Tolerance);
        Assert.Equal(MathHelper.PiOver4, VerticalFieldOfView(camera), Tolerance);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
