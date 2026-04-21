using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class MorphVertexApplicatorTests
{
    [Fact]
    public void MorphVertexApplicator_AppliesTargetDeltasAndNormalizesDirections()
    {
        var sourceVertices = new[]
        {
            new VertexPositionTextureNormalTangentWeights
            {
                Position = Vector3.Zero,
                Normal = Vector3.UnitX,
                Tangent = Vector3.UnitY,
                BiTangent = Vector3.UnitZ,
                TextureCoordinate = Vector2.Zero,
                Color = Vector4.One,
            },
        };
        var destinationVertices = new VertexPositionTextureNormalTangentWeights[sourceVertices.Length];
        var morphTargets = new[]
        {
            new MorphTarget(
                0,
                0,
                "Face",
                "Smile",
                0f,
                positions: new[] { new Vector3(2f, 0f, 0f) },
                normals: new[] { Vector3.UnitY },
                tangents: new[] { Vector3.UnitZ },
                biTangents: new[] { Vector3.UnitX },
                textureCoordinateChannels: new[] { new[] { new Vector3(1f, 0.5f, 0f) } },
                vertexColorChannels: new[] { new[] { new Vector4(0f, 0.5f, 1f, 1f) } }),
        };
        var attachmentWeights = new[] { 0.25f };

        var applied = MorphVertexApplicator.Apply(sourceVertices, morphTargets, attachmentWeights, destinationVertices);

        Assert.True(applied);
        Assert.Equal(new Vector3(0.5f, 0f, 0f), destinationVertices[0].Position);
        Assert.Equal(new Vector2(0.25f, 0.125f), destinationVertices[0].TextureCoordinate);
        Assert.Equal(new Vector4(0.75f, 0.875f, 1f, 1f), destinationVertices[0].Color);
        Assert.Equal(1f, destinationVertices[0].Normal.Length(), 3);
        Assert.Equal(1f, destinationVertices[0].Tangent.Length(), 3);
        Assert.Equal(1f, destinationVertices[0].BiTangent.Length(), 3);
        Assert.True(destinationVertices[0].Normal.Y > 0f);
        Assert.True(destinationVertices[0].Tangent.Z > 0f);
        Assert.True(destinationVertices[0].BiTangent.X > 0f);
    }
}