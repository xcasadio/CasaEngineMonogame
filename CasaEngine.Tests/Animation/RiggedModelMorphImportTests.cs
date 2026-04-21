using Assimp;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class RiggedModelMorphImportTests
{
    [Fact]
    public void ExtractMorphRuntimeData_ConvertsMeshAttachmentsAndMorphChannels()
    {
        var scene = new Assimp.Scene();
        var mesh = new Assimp.Mesh("Face", PrimitiveType.Triangle);
        var attachment = new Assimp.MeshAnimationAttachment
        {
            Weight = 0.5f,
        };
        attachment.Vertices.Add(new Vector3D(1f, 2f, 3f));
        attachment.Normals.Add(new Vector3D(0f, 1f, 0f));
        attachment.TextureCoordinateChannels[0].Add(new Vector3D(0.25f, 0.75f, 0f));
        mesh.MeshAnimationAttachments.Add(attachment);
        scene.Meshes.Add(mesh);

        var animation = new Assimp.Animation
        {
            Name = "Blink",
            DurationInTicks = 24d,
            TicksPerSecond = 24d,
        };
        var channel = new Assimp.MeshMorphAnimationChannel
        {
            Name = "Face",
        };
        var key = new Assimp.MeshMorphKey
        {
            Time = 12d,
        };
        key.Values.Add(0);
        key.Weights.Add(0.75d);
        channel.MeshMorphKeys.Add(key);
        animation.MeshMorphAnimationChannels.Add(channel);
        scene.Animations.Add(animation);

        var (morphTargets, morphClips) = RiggedModelLoader.ExtractMorphRuntimeData(scene);

        var target = Assert.Single(morphTargets);
        Assert.Equal(0, target.MeshIndex);
        Assert.Equal(0, target.AttachmentIndex);
        Assert.Equal("Face", target.MeshName);
        Assert.Equal("Face.Morph0", target.Name);
        Assert.Equal(0.5f, target.DefaultWeight);
        Assert.Equal(new Vector3(1f, 2f, 3f), Assert.Single(target.Positions));
        Assert.Equal(new Vector3(0f, 1f, 0f), Assert.Single(target.Normals));
        Assert.Single(target.TextureCoordinateChannels);
        Assert.Equal(new Vector3(0.25f, 0.75f, 0f), Assert.Single(target.TextureCoordinateChannels[0]));

        var clip = Assert.Single(morphClips);
        Assert.Equal("Blink", clip.Name);
        Assert.Equal(1f, clip.DurationSeconds);

        var morphChannel = Assert.Single(clip.Channels);
        Assert.Equal("Face", morphChannel.MeshName);
        Assert.Equal(0, morphChannel.MeshIndex);

        var keyframe = Assert.Single(morphChannel.Keyframes);
        Assert.Equal(0.5f, keyframe.TimeSeconds);
        Assert.Equal(0, Assert.Single(keyframe.AttachmentIndices));
        Assert.Equal(0.75f, Assert.Single(keyframe.Weights), 3);
    }
}