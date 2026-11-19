using System.Globalization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The two camera entities of the repository that named an arc ball camera (ADR-0149). The view and the projection of
/// each one were recorded from the files as they stood before the migration, with the old class; the files must keep
/// giving them. This test names no concrete camera class: it is the same before and after the migration.
/// </summary>
public sealed class LegacyCameraEntityDataTests
{
    //  The tolerance of OrbitCameraComponentTests: before the migration the view came from CreateLookAt, after it comes
    //  from the inverse of the world matrix, and their last bits differ.
    private const float Tolerance = 1e-4f;

    //  Recorded on 2026-11-19 from both files (their camera nodes are identical), viewport 1280 x 720, depths 1 and 1000.
    private static readonly float[] ExpectedView =
    [
        -0.964125872f, 0.135757655f, -0.228103638f, 0f,
        2.23517418E-08f, 0.859323382f, 0.511432827f, 0f,
        0.265445679f, 0.493085623f, -0.8284958f, 0f,
        -6.93412876f, 4.44501209f, -8.6817112f, 1f,
    ];

    private static readonly float[] ExpectedProjection =
    [
        1.35799503f, 0f, 0f, 0f,
        0f, 2.41421342f, 0f, 0f,
        0f, 0f, -1.001001f, -1f,
        0f, 0f, -1.001001f, 0f,
    ];

    [Theory]
    [InlineData("CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity")]
    [InlineData("Projects/SampleProject/Entities/camera.entity")]
    public void TheSavedCamera_GivesTheViewAndTheProjectionRecordedBeforeTheMigration(string relativePath)
    {
        CameraComponent camera = LoadCamera(JObject.Parse(File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath))));

        AssertTerms(ExpectedView, camera.ViewMatrix, "view");
        AssertTerms(ExpectedProjection, camera.ProjectionMatrix, "projection");
    }

    /// <summary>Loads the entity as a world does, initialises it, and sizes its camera.</summary>
    internal static CameraComponent LoadCamera(JObject entityNode)
    {
        var entity = new Entity();
        entity.Load(entityNode);
        entity.Initialize();

        CameraComponent camera = entity.GetComponent<CameraComponent>();
        Assert.NotNull(camera);
        // Without a world the viewport is empty: the far plane first, since the near plane is clamped on it.
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(1280, 720);
        return camera;
    }

    internal static void AssertTerms(float[] expected, Matrix actual, string what)
    {
        float[] actualTerms = Matrix.ToFloatArray(actual);
        Assert.True(expected.Length == actualTerms.Length,
            $"{what}: " + string.Join(", ", actualTerms.Select(term => term.ToString("G9", CultureInfo.InvariantCulture) + "f")));
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.True(MathF.Abs(expected[index] - actualTerms[index]) <= Tolerance,
                $"{what}, term {index}: expected {expected[index]}, actual {actualTerms[index]}");
        }
    }

    internal static string FindRepositoryRoot()
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
