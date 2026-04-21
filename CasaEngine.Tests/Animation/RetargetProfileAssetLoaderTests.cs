using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

[Collection(ProjectEnvironmentCollection.Name)]
public class RetargetProfileAssetLoaderTests
{
    [Fact]
    public void AssetLoaderRegistry_LoadsRetargetProfileAndResolvesJointMappings()
    {
        string projectDirectory = CreateTempDirectory();
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = projectDirectory;
            EditorAssetCatalogService.Clear();

            Guid sourceSkeletonAssetId = Guid.NewGuid();
            Guid targetSkeletonAssetId = Guid.NewGuid();
            Guid retargetProfileAssetId = Guid.NewGuid();

            var sourceSkeletonAsset = new SkeletonAsset
            {
                Name = "SourceSkeleton",
                FileName = "Animation/Source.skeleton",
            };
            sourceSkeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = "Root",
                ParentIndex = -1,
                LocalBindTransform = BoneTransform.Identity,
                InverseBindMatrix = Microsoft.Xna.Framework.Matrix.Identity,
                SkinPaletteIndex = 0,
            });
            sourceSkeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = "Hand_R",
                ParentIndex = 0,
                LocalBindTransform = BoneTransform.Identity,
                InverseBindMatrix = Microsoft.Xna.Framework.Matrix.Identity,
                SkinPaletteIndex = 1,
            });

            var targetSkeletonAsset = new SkeletonAsset
            {
                Name = "TargetSkeleton",
                FileName = "Animation/Target.skeleton",
            };
            targetSkeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = "Pelvis",
                ParentIndex = -1,
                LocalBindTransform = BoneTransform.Identity,
                InverseBindMatrix = Microsoft.Xna.Framework.Matrix.Identity,
                SkinPaletteIndex = 0,
            });
            targetSkeletonAsset.Joints.Add(new SkeletonJointAsset
            {
                Name = "RightHand",
                ParentIndex = 0,
                LocalBindTransform = BoneTransform.Identity,
                InverseBindMatrix = Microsoft.Xna.Framework.Matrix.Identity,
                SkinPaletteIndex = 1,
            });

            var retargetProfileAsset = new RetargetProfileAsset
            {
                Name = "SourceToTarget",
                FileName = "Animation/SourceToTarget.retargetProfile",
                SourceSkeletonAssetId = sourceSkeletonAssetId,
                TargetSkeletonAssetId = targetSkeletonAssetId,
                ReferencePoseMode = RetargetReferencePoseMode.BindPose,
                SourceForwardAxis = RetargetAxis.PositiveZ,
                SourceUpAxis = RetargetAxis.PositiveY,
                TargetForwardAxis = RetargetAxis.PositiveX,
                TargetUpAxis = RetargetAxis.PositiveY,
                RootTranslationScale = 0.01f,
            };
            retargetProfileAsset.JointMappings.Add(new RetargetJointMappingAsset
            {
                SourceJointName = "Root",
                TargetJointName = "Pelvis",
                TranslationScale = 0.01f,
            });
            retargetProfileAsset.JointMappings.Add(new RetargetJointMappingAsset
            {
                SourceJointName = "Hand_R",
                TargetJointName = "RightHand",
                TranslationScale = 1f,
            });

            JObject sourceSkeletonDocument = new();
            SkeletonAssetJsonSerializer.Save(sourceSkeletonAsset, sourceSkeletonDocument);
            sourceSkeletonDocument["id"] = sourceSkeletonAssetId.ToString();
            sourceSkeletonDocument["name"] = sourceSkeletonAsset.Name;

            JObject targetSkeletonDocument = new();
            SkeletonAssetJsonSerializer.Save(targetSkeletonAsset, targetSkeletonDocument);
            targetSkeletonDocument["id"] = targetSkeletonAssetId.ToString();
            targetSkeletonDocument["name"] = targetSkeletonAsset.Name;

            JObject retargetProfileDocument = new();
            RetargetProfileAssetJsonSerializer.Save(retargetProfileAsset, retargetProfileDocument);
            retargetProfileDocument["id"] = retargetProfileAssetId.ToString();
            retargetProfileDocument["name"] = retargetProfileAsset.Name;

            WriteDocument(projectDirectory, sourceSkeletonAsset.FileName, sourceSkeletonDocument);
            WriteDocument(projectDirectory, targetSkeletonAsset.FileName, targetSkeletonDocument);
            WriteDocument(projectDirectory, retargetProfileAsset.FileName, retargetProfileDocument);

            EditorAssetCatalogService.Add(new AssetInfo(sourceSkeletonAssetId)
            {
                Name = sourceSkeletonAsset.Name,
                FileName = sourceSkeletonAsset.FileName,
                AssetType = AssetInfo.InferAssetType(sourceSkeletonAsset.FileName),
            });
            EditorAssetCatalogService.Add(new AssetInfo(targetSkeletonAssetId)
            {
                Name = targetSkeletonAsset.Name,
                FileName = targetSkeletonAsset.FileName,
                AssetType = AssetInfo.InferAssetType(targetSkeletonAsset.FileName),
            });
            EditorAssetCatalogService.Add(new AssetInfo(retargetProfileAssetId)
            {
                Name = retargetProfileAsset.Name,
                FileName = retargetProfileAsset.FileName,
                AssetType = AssetInfo.InferAssetType(retargetProfileAsset.FileName),
            });

            var assetContentManager = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assetContentManager);

            var retargetProfile = assetContentManager.Load<RetargetProfile>(retargetProfileAssetId);

            Assert.Equal(RetargetReferencePoseMode.BindPose, retargetProfile.ReferencePoseMode);
            Assert.Equal(RetargetAxis.PositiveZ, retargetProfile.SourceForwardAxis);
            Assert.Equal(RetargetAxis.PositiveX, retargetProfile.TargetForwardAxis);
            Assert.Equal(0.01f, retargetProfile.RootTranslationScale);
            Assert.Equal(2, retargetProfile.JointMappings.Count);
            Assert.True(retargetProfile.TryGetTargetJointIndex(0, out var pelvisIndex));
            Assert.Equal(0, pelvisIndex);
            Assert.True(retargetProfile.TryGetTargetJointIndex(1, out var rightHandIndex));
            Assert.Equal(1, rightHandIndex);
            Assert.True(retargetProfile.TryGetJointMapping(0, out var rootMapping));
            Assert.Equal(0.01f, rootMapping.TranslationScale);
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