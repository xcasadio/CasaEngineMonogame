using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Guards the multi-skin three.js Soldier asset (2 skins, 4 clips). Loading is via SharpGLTF,
/// so the model must come in Y-up, standing, at its native ~1.83 unit scale (this is what the
/// SkeletalAnimationBlendingDemo transform relies on).
/// </summary>
public class SoldierGlbRegressionTests
{
    [Fact]
    public void Soldier_LoadsMultiSkinSkeletonClipsAndYUpBindPose()
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "CasaEngine.Demos", "Content", "SkinnedMesh", "Soldier.glb");
        Assert.True(File.Exists(path), $"missing {path}");

        var rigged = new GltfRiggedModelReader().LoadAsset(path);

        Assert.NotNull(rigged);
        Assert.True(rigged!.NumberOfBonesInUse > 1, "Soldier lost its skeleton.");
        Assert.Equal(4, rigged.AnimationClips.Count);
        Assert.Equal(2, rigged.Meshes.Length);
        Assert.All(rigged.Meshes, mesh => Assert.True(mesh.Vertices.Length > 0));

        // Bind-pose joint AABB: Y must be the dominant (vertical) extent, and the model
        // ~1.8 units tall standing upright (not lying along Z, not at cm scale).
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var node in rigged.FlatListToAllNodes)
        {
            var t = node.CombinedTransformMg.Translation;
            min = Vector3.Min(min, t);
            max = Vector3.Max(max, t);
        }
        var size = max - min;

        Assert.InRange(size.Y, 1.5f, 2.2f);
        Assert.True(size.Y > size.Z, "Soldier should be standing (Y) not lying (Z).");
        Assert.InRange(min.Y, -0.05f, 0.05f); // feet at the model origin
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repo root not found");
    }
}
