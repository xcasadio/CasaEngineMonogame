using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The data of the 2d camera after ADR-0150: the pose comes from the transform, the editor no longer writes
/// <c>target</c>, a <c>target</c> key left in a file is reported and ignored, and no world of the repository keeps one.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class Camera2dTargetKeyTests : IDisposable
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private readonly CapturingLogger _logger = new();

    public Camera2dTargetKeyTests()
    {
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        Logs.Close();
    }

    private static JObject SavedCameraNode()
    {
        var camera = new Camera2dComponent { Zoom = 2f, PixelSnap = true };
        camera.CentreOn(new Vector3(12f, 34f, 0f));
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(new Entity { RootComponent = camera }, node);
        return (JObject)node["root_component"]!;
    }

    [Fact]
    public void TheEditorWriter_WritesNoTargetKey_AndTheCameraRoundTrips()
    {
        JObject node = SavedCameraNode();

        Assert.False(node.ContainsKey("target"));

        var loaded = Assert.IsType<Camera2dComponent>(ElementFactory.LoadComponent<SceneComponent>(node));
        Assert.Equal(new Vector3(12f, 34f, 500f), loaded.LocalPosition);
        Assert.Equal(2f, loaded.Zoom);
        Assert.True(loaded.PixelSnap);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void ATargetKeyLeftInAFile_IsReportedOnce_AndThePoseComesFromTheTransform()
    {
        JObject node = SavedCameraNode();
        node["target"] = new JObject { ["x"] = 900f, ["y"] = 800f, ["z"] = 700f };

        var loaded = Assert.IsType<Camera2dComponent>(ElementFactory.LoadComponent<SceneComponent>(node));

        Assert.Equal(new Vector3(12f, 34f, 500f), loaded.LocalPosition);
        string error = Assert.Single(_logger.Errors);
        Assert.Contains("'target'", error);
        Assert.Contains((string)node["id"]!, error);
    }

    [Theory]
    [MemberData(nameof(Camera2dWorldDataTests.Worlds), MemberType = typeof(Camera2dWorldDataTests))]
    public void TheCameraOfAWorldOfTheRepository_LoadsWithoutAnyReport(string relativePath)
    {
        JObject node = Camera2dWorldDataTests.FindCameraNode(relativePath);

        Assert.IsType<Camera2dComponent>(ElementFactory.LoadComponent<SceneComponent>(node));

        Assert.False(node.ContainsKey("target"));
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void NoDataFileOfTheRepository_KeepsATargetKeyUnderA2dCamera()
    {
        string root = LegacyCameraEntityDataTests.FindRepositoryRoot();
        var offenders = new List<string>();

        foreach (string directory in new[] { Path.Combine(root, "Projects"), Path.Combine(root, "CasaEngine.Demos", "Content") })
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                bool isData = extension is ".world" or ".entity";
                bool isBuildOutput = file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);
                if (!isData || isBuildOutput)
                {
                    continue;
                }

                JToken document = JToken.Parse(File.ReadAllText(file));
                bool keepsTarget = document.SelectTokens("$..*")
                    .OfType<JObject>()
                    .Any(node => (string)node["type"] == nameof(Camera2dComponent) && node.ContainsKey("target"));
                if (keepsTarget)
                {
                    offenders.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        Assert.Empty(offenders);
    }
}
