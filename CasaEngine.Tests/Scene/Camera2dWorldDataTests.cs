using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The worlds of the repository that hold a 2d camera (ADR-0150). The view and the projection of each camera were
/// recorded before the camera was posed by its transform: every world had a zero target, a zoom of 1 and no pixel snap,
/// so every camera stood at (0, 0, 500) looking at the origin. The worlds must keep giving that view.
/// </summary>
public sealed class Camera2dWorldDataTests
{
    private const float Tolerance = 1e-4f;

    public static TheoryData<string> Worlds => new()
    {
        "Projects/BufferedResolutionSample/TitleWorld.world",
        "Projects/PixelUiSample/BlackWorld.world",
        "Projects/PixelUiSample/TitleWorld.world",
        "Projects/RPGDemo/DefaultWorld.world",
        "Projects/SpriteCutsceneSample/EndWorld.world",
        "Projects/SpriteCutsceneSample/SpriteWorld.world",
        "Projects/SpriteDrawOrderSample/DrawOrderWorld.world",
        "Projects/TimedScreenSample/FirstWorld.world",
        "Projects/TimedScreenSample/SecondWorld.world",
        "Projects/VideoSample/VideoWorld.world",
        "Projects/VideoSpriteExitSample/EndWorld.world",
        "Projects/VideoSpriteExitSample/ExitFilmWorld.world",
        "Projects/VideoSpriteSample/EndWorld.world",
        "Projects/VideoSpriteSample/VideoSpriteWorld.world",
    };

    [Theory]
    [MemberData(nameof(Worlds))]
    public void TheCameraOfTheWorld_GivesTheViewAndTheProjectionRecordedBeforeADR0150(string relativePath)
    {
        JObject cameraNode = FindCameraNode(relativePath);
        var camera = Assert.IsType<Camera2dComponent>(ElementFactory.LoadComponent<SceneComponent>(cameraNode));
        // Without a world the viewport keeps its saved size: the far plane first, since the near plane is clamped on it.
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(1280, 720);

        Matrix expectedView = Matrix.CreateLookAt(new Vector3(0f, 0f, 500f), Vector3.Zero, Vector3.Up);
        Matrix expectedProjection = Matrix.CreateOrthographic(1280f, 720f, 1f, 1000f);

        AssertMatrixEqual(expectedView, camera.ViewMatrix, "view");
        AssertMatrixEqual(expectedProjection, camera.ProjectionMatrix, "projection");
    }

    internal static JObject FindCameraNode(string relativePath)
    {
        JToken world = JToken.Parse(File.ReadAllText(Path.Combine(LegacyCameraEntityDataTests.FindRepositoryRoot(), relativePath)));
        JObject[] cameras = world.SelectTokens("$..*")
            .OfType<JObject>()
            .Where(node => (string)node["type"] == nameof(Camera2dComponent))
            .ToArray();

        return Assert.Single(cameras);
    }

    private static void AssertMatrixEqual(Matrix expected, Matrix actual, string what)
    {
        float[] expectedTerms = Matrix.ToFloatArray(expected);
        float[] actualTerms = Matrix.ToFloatArray(actual);
        for (int index = 0; index < expectedTerms.Length; index++)
        {
            Assert.True(MathF.Abs(expectedTerms[index] - actualTerms[index]) <= Tolerance,
                $"{what}, term {index}: expected {expectedTerms[index]}, actual {actualTerms[index]}");
        }
    }
}
