using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class AuthoringAssetJsonSerializerTests
{
    [Fact]
    public void SkeletonAssetSerializer_RoundTripsSkeletonAuthoringData()
    {
        var skeletonAsset = new SkeletonAsset
        {
            Name = "Hero Skeleton",
        };
        skeletonAsset.Joints.Add(new SkeletonJointAsset
        {
            Name = "root",
            ParentIndex = -1,
            LocalBindTransform = new BoneTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, Vector3.One),
            InverseBindMatrix = Matrix.CreateTranslation(-1.0f, -2.0f, -3.0f),
            SkinPaletteIndex = 0,
        });
        skeletonAsset.Joints.Add(new SkeletonJointAsset
        {
            Name = "spine",
            ParentIndex = 0,
            LocalBindTransform = new BoneTransform(new Vector3(0.0f, 10.0f, 0.0f), Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f), Vector3.One),
            InverseBindMatrix = Matrix.CreateTranslation(0.0f, -10.0f, 0.0f),
            SkinPaletteIndex = 1,
        });

        var document = new JObject();
        SkeletonAssetJsonSerializer.Save(skeletonAsset, document);

        var loadedSkeletonAsset = new SkeletonAsset();
        loadedSkeletonAsset.Load(document);

        Assert.Equal(skeletonAsset.Id, loadedSkeletonAsset.Id);
        Assert.Equal(skeletonAsset.Name, loadedSkeletonAsset.Name);
        Assert.Equal(2, loadedSkeletonAsset.Joints.Count);
        Assert.Equal("root", loadedSkeletonAsset.Joints[0].Name);
        Assert.Equal(-1, loadedSkeletonAsset.Joints[0].ParentIndex);
        Assert.Equal(skeletonAsset.Joints[0].LocalBindTransform, loadedSkeletonAsset.Joints[0].LocalBindTransform);
        Assert.Equal(skeletonAsset.Joints[0].InverseBindMatrix, loadedSkeletonAsset.Joints[0].InverseBindMatrix);
        Assert.Equal(0, loadedSkeletonAsset.Joints[0].SkinPaletteIndex);
        Assert.Equal("spine", loadedSkeletonAsset.Joints[1].Name);
        Assert.Equal(0, loadedSkeletonAsset.Joints[1].ParentIndex);
        Assert.Equal(skeletonAsset.Joints[1].LocalBindTransform, loadedSkeletonAsset.Joints[1].LocalBindTransform);
        Assert.Equal(skeletonAsset.Joints[1].InverseBindMatrix, loadedSkeletonAsset.Joints[1].InverseBindMatrix);
        Assert.Equal(1, loadedSkeletonAsset.Joints[1].SkinPaletteIndex);
    }

    [Fact]
    public void AnimationClipAssetSerializer_RoundTripsClipAuthoringData()
    {
        var clipAsset = new AnimationClipAsset
        {
            Name = "Run",
            SkeletonAssetId = Guid.NewGuid(),
            DurationSeconds = 1.25f,
        };

        var rootTrack = new AnimationJointTrackAsset
        {
            JointName = "root",
        };
        var targetRotation = Quaternion.CreateFromAxisAngle(Vector3.Up, 0.25f);
        rootTrack.TranslationKeyframes.Add(new Vector3AnimationKeyframeAsset(0.0f, Vector3.Zero));
        rootTrack.TranslationKeyframes.Add(new Vector3AnimationKeyframeAsset(1.25f, new Vector3(3.0f, 0.0f, 0.0f)));
        rootTrack.RotationKeyframes.Add(new QuaternionAnimationKeyframeAsset(0.0f, Quaternion.Identity));
        rootTrack.RotationKeyframes.Add(new QuaternionAnimationKeyframeAsset(1.25f, targetRotation));
        rootTrack.ScaleKeyframes.Add(new Vector3AnimationKeyframeAsset(0.0f, Vector3.One));
        clipAsset.JointTracks.Add(rootTrack);
        clipAsset.Events.Add(new AnimationEventAsset(0.5f, "footstep_left"));
        clipAsset.Events.Add(new AnimationEventAsset(1.0f, "footstep_right"));

        var document = new JObject();
        AnimationClipAssetJsonSerializer.Save(clipAsset, document);

        var loadedClipAsset = new AnimationClipAsset();
        loadedClipAsset.Load(document);

        Assert.Equal(clipAsset.Id, loadedClipAsset.Id);
        Assert.Equal(clipAsset.Name, loadedClipAsset.Name);
        Assert.Equal(clipAsset.SkeletonAssetId, loadedClipAsset.SkeletonAssetId);
        Assert.Equal(clipAsset.DurationSeconds, loadedClipAsset.DurationSeconds);
        Assert.Single(loadedClipAsset.JointTracks);
        Assert.Equal("root", loadedClipAsset.JointTracks[0].JointName);
        Assert.Equal(2, loadedClipAsset.JointTracks[0].TranslationKeyframes.Count);
        Assert.Equal(2, loadedClipAsset.JointTracks[0].RotationKeyframes.Count);
        Assert.Single(loadedClipAsset.JointTracks[0].ScaleKeyframes);
        Assert.Equal(new Vector3(3.0f, 0.0f, 0.0f), loadedClipAsset.JointTracks[0].TranslationKeyframes[1].Value);
        Assert.Equal(targetRotation, loadedClipAsset.JointTracks[0].RotationKeyframes[1].Value);
        Assert.Equal(2, loadedClipAsset.Events.Count);
        Assert.Equal(new AnimationEventAsset(0.5f, "footstep_left"), loadedClipAsset.Events[0]);
        Assert.Equal(new AnimationEventAsset(1.0f, "footstep_right"), loadedClipAsset.Events[1]);
    }

    [Fact]
    public void SkinnedMeshAssetSerializer_LoadsLegacyRiggedModelFieldAsGeometryReference()
    {
        var skeletonAssetId = Guid.NewGuid();
        var legacyRiggedModelAssetId = Guid.NewGuid();
        var clipAssetId = Guid.NewGuid();
        var defaultClipAssetId = Guid.NewGuid();

        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Hero Mesh",
            ["skeleton_asset_id"] = skeletonAssetId.ToString(),
            ["rigged_model_asset_id"] = legacyRiggedModelAssetId.ToString(),
            ["default_animation_clip_asset_id"] = defaultClipAssetId.ToString(),
            ["animation_clip_asset_ids"] = new JArray
            {
                clipAssetId.ToString(),
            },
        };

        var skinnedMeshAsset = new SkinnedMeshAsset();
        skinnedMeshAsset.Load(document);

        Assert.Equal("Hero Mesh", skinnedMeshAsset.Name);
        Assert.Equal(skeletonAssetId, skinnedMeshAsset.SkeletonAssetId);
        Assert.Equal(legacyRiggedModelAssetId, skinnedMeshAsset.GeometryAssetId);
        Assert.Equal(defaultClipAssetId, skinnedMeshAsset.DefaultAnimationClipAssetId);
        Assert.Single(skinnedMeshAsset.AnimationClipAssetIds);
        Assert.Equal(clipAssetId, skinnedMeshAsset.AnimationClipAssetIds[0]);
    }
}