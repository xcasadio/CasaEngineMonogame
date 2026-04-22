using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Rendering.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

[Collection(ProjectEnvironmentCollection.Name)]
public class AnimationAssetLoaderTests
{
    [Fact]
    public void AssetLoaderRegistry_LoadsSkeletonClipAndModernModelWrapper()
    {
        string projectDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();

            Guid skeletonAssetId = Guid.NewGuid();
            Guid clipAssetId = Guid.NewGuid();
            Guid modelAssetId = Guid.NewGuid();
            Guid geometryAssetId = Guid.NewGuid();

            var skeletonAsset = new SkeletonAsset
            {
                Name = "HeroSkeleton",
                FileName = "Animation/Hero.skeleton",
            };
            skeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = "Root",
                ParentIndex = -1,
                LocalBindTransform = BoneTransform.Identity,
                InverseBindMatrix = Microsoft.Xna.Framework.Matrix.Identity,
                SkinPaletteIndex = 0,
            });

            var clipAsset = new AnimationClipAsset
            {
                Name = "Idle",
                FileName = "Animation/Hero_Idle.skeletonAnim",
                SkeletonAssetId = skeletonAssetId,
                DurationSeconds = 1.0f,
            };
            var jointTrack = new AnimationJointTrackAsset
            {
                JointName = "Root",
            };
            jointTrack.TranslationKeyframes.Add(new Vector3AnimationKeyframeAsset(0f, Microsoft.Xna.Framework.Vector3.Zero));
            jointTrack.TranslationKeyframes.Add(new Vector3AnimationKeyframeAsset(1.0f, new Microsoft.Xna.Framework.Vector3(1f, 0f, 0f)));
            clipAsset.JointTracks.Add(jointTrack);
            clipAsset.Events.Add(new AnimationEventAsset(0.5f, "Step"));

            JObject skeletonDocument = new();
            SkeletonAssetJsonSerializer.Save(skeletonAsset, skeletonDocument);
            skeletonDocument["id"] = skeletonAssetId.ToString();
            skeletonDocument["name"] = skeletonAsset.Name;

            JObject clipDocument = new();
            AnimationClipAssetJsonSerializer.Save(clipAsset, clipDocument);
            clipDocument["id"] = clipAssetId.ToString();
            clipDocument["name"] = clipAsset.Name;

            var modelDocument = new JObject
            {
                ["id"] = modelAssetId.ToString(),
                ["name"] = "Hero",
                ["skeleton_asset_id"] = skeletonAssetId.ToString(),
                ["geometry_asset_id"] = geometryAssetId.ToString(),
                ["default_animation_clip_asset_id"] = clipAssetId.ToString(),
                ["animation_clip_asset_ids"] = new JArray(clipAssetId.ToString()),
            };

            WriteDocument(projectDirectory, skeletonAsset.FileName, skeletonDocument);
            WriteDocument(projectDirectory, clipAsset.FileName, clipDocument);
            WriteDocument(projectDirectory, "Hero.model", modelDocument);

            EditorAssetCatalogService.Add(new AssetInfo(skeletonAssetId)
            {
                Name = skeletonAsset.Name,
                FileName = skeletonAsset.FileName,
                AssetType = AssetInfo.InferAssetType(skeletonAsset.FileName),
            });
            EditorAssetCatalogService.Add(new AssetInfo(clipAssetId)
            {
                Name = clipAsset.Name,
                FileName = clipAsset.FileName,
                AssetType = AssetInfo.InferAssetType(clipAsset.FileName),
            });
            EditorAssetCatalogService.Add(new AssetInfo(modelAssetId)
            {
                Name = "Hero",
                FileName = "Hero.model",
                AssetType = AssetInfo.InferAssetType("Hero.model"),
            });
            EditorAssetCatalogService.Add(new AssetInfo(geometryAssetId)
            {
                Name = "HeroRaw",
                FileName = "Hero.fbx",
                AssetType = AssetInfo.InferAssetType("Hero.fbx"),
            });

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            var skeletonDefinition = assetContentManager.Load<SkeletonDefinition>(skeletonAssetId);
            var animationClip = assetContentManager.Load<AnimationClip>(clipAssetId);
            var skinnedMesh = assetContentManager.Load<SkinnedMesh>(modelAssetId, cache: false);

            Assert.Equal(1, skeletonDefinition.Count);
            Assert.Equal("Root", skeletonDefinition.GetJoint(0).Name);
            Assert.Equal(1.0f, animationClip.DurationSeconds);
            Assert.True(ReferenceEquals(animationClip.Skeleton, skeletonDefinition));
            Assert.True(animationClip.TryGetJointTrack(0, out var runtimeTrack));
            Assert.NotNull(runtimeTrack);
            Assert.Equal(geometryAssetId, skinnedMesh.RiggedModelAssetId);
            Assert.Equal(skeletonAssetId, skinnedMesh.SkeletonAssetId);
            Assert.Equal(clipAssetId, skinnedMesh.DefaultAnimationClipAssetId);
            Assert.Single(skinnedMesh.AnimationClipAssetIds);
            Assert.Equal(clipAssetId, skinnedMesh.AnimationClipAssetIds[0]);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteDocument(string projectDirectory, string relativePath, JObject document)
    {
        string fullPath = Path.Combine(projectDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, document.ToString());
    }
}