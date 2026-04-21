using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class MorphVertexApplicator
{
    public static bool Apply(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        IReadOnlyList<MorphTarget> morphTargets,
        IReadOnlyList<float> attachmentWeights,
        VertexPositionTextureNormalTangentWeights[] destinationVertices)
    {
        ArgumentNullException.ThrowIfNull(sourceVertices);
        ArgumentNullException.ThrowIfNull(morphTargets);
        ArgumentNullException.ThrowIfNull(attachmentWeights);
        ArgumentNullException.ThrowIfNull(destinationVertices);

        if (destinationVertices.Length != sourceVertices.Length)
        {
            throw new ArgumentException("The destination vertex buffer must match the source vertex count.", nameof(destinationVertices));
        }

        Array.Copy(sourceVertices, destinationVertices, sourceVertices.Length);

        var hasActiveMorph = false;
        var normalizeNormals = false;
        var normalizeTangents = false;
        var normalizeBiTangents = false;

        for (var morphTargetIndex = 0; morphTargetIndex < morphTargets.Count; morphTargetIndex++)
        {
            var morphTarget = morphTargets[morphTargetIndex] ?? throw new ArgumentException("Morph targets cannot contain null entries.", nameof(morphTargets));
            if ((uint)morphTarget.AttachmentIndex >= (uint)attachmentWeights.Count)
            {
                continue;
            }

            var weight = attachmentWeights[morphTarget.AttachmentIndex];
            if (Math.Abs(weight) <= float.Epsilon)
            {
                continue;
            }

            hasActiveMorph = true;
            normalizeNormals |= morphTarget.Normals.Count > 0;
            normalizeTangents |= morphTarget.Tangents.Count > 0;
            normalizeBiTangents |= morphTarget.BiTangents.Count > 0;

            ApplyPositionMorph(sourceVertices, destinationVertices, morphTarget, weight);
            ApplyNormalMorph(sourceVertices, destinationVertices, morphTarget, weight);
            ApplyTangentMorph(sourceVertices, destinationVertices, morphTarget, weight);
            ApplyBiTangentMorph(sourceVertices, destinationVertices, morphTarget, weight);
            ApplyTextureCoordinateMorph(sourceVertices, destinationVertices, morphTarget, weight);
            ApplyVertexColorMorph(sourceVertices, destinationVertices, morphTarget, weight);
        }

        if (!hasActiveMorph)
        {
            return false;
        }

        NormalizeDirectionVectors(destinationVertices, normalizeNormals, normalizeTangents, normalizeBiTangents);
        return true;
    }

    private static void ApplyPositionMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        var vertexCount = Math.Min(sourceVertices.Length, morphTarget.Positions.Count);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            destinationVertices[vertexIndex].Position += (morphTarget.Positions[vertexIndex] - sourceVertices[vertexIndex].Position) * weight;
        }
    }

    private static void ApplyNormalMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        var vertexCount = Math.Min(sourceVertices.Length, morphTarget.Normals.Count);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            destinationVertices[vertexIndex].Normal += (morphTarget.Normals[vertexIndex] - sourceVertices[vertexIndex].Normal) * weight;
        }
    }

    private static void ApplyTangentMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        var vertexCount = Math.Min(sourceVertices.Length, morphTarget.Tangents.Count);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            destinationVertices[vertexIndex].Tangent += (morphTarget.Tangents[vertexIndex] - sourceVertices[vertexIndex].Tangent) * weight;
        }
    }

    private static void ApplyBiTangentMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        var vertexCount = Math.Min(sourceVertices.Length, morphTarget.BiTangents.Count);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            destinationVertices[vertexIndex].BiTangent += (morphTarget.BiTangents[vertexIndex] - sourceVertices[vertexIndex].BiTangent) * weight;
        }
    }

    private static void ApplyTextureCoordinateMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        if (morphTarget.TextureCoordinateChannels.Count == 0)
        {
            return;
        }

        var textureCoordinates = morphTarget.TextureCoordinateChannels[0];
        var vertexCount = Math.Min(sourceVertices.Length, textureCoordinates.Length);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var targetTextureCoordinate = new Vector2(textureCoordinates[vertexIndex].X, textureCoordinates[vertexIndex].Y);
            destinationVertices[vertexIndex].TextureCoordinate += (targetTextureCoordinate - sourceVertices[vertexIndex].TextureCoordinate) * weight;
        }
    }

    private static void ApplyVertexColorMorph(
        VertexPositionTextureNormalTangentWeights[] sourceVertices,
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        MorphTarget morphTarget,
        float weight)
    {
        if (morphTarget.VertexColorChannels.Count == 0)
        {
            return;
        }

        var vertexColors = morphTarget.VertexColorChannels[0];
        var vertexCount = Math.Min(sourceVertices.Length, vertexColors.Length);
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            destinationVertices[vertexIndex].Color += (vertexColors[vertexIndex] - sourceVertices[vertexIndex].Color) * weight;
        }
    }

    private static void NormalizeDirectionVectors(
        VertexPositionTextureNormalTangentWeights[] destinationVertices,
        bool normalizeNormals,
        bool normalizeTangents,
        bool normalizeBiTangents)
    {
        for (var vertexIndex = 0; vertexIndex < destinationVertices.Length; vertexIndex++)
        {
            if (normalizeNormals && destinationVertices[vertexIndex].Normal.LengthSquared() > float.Epsilon)
            {
                destinationVertices[vertexIndex].Normal = Vector3.Normalize(destinationVertices[vertexIndex].Normal);
            }

            if (normalizeTangents && destinationVertices[vertexIndex].Tangent.LengthSquared() > float.Epsilon)
            {
                destinationVertices[vertexIndex].Tangent = Vector3.Normalize(destinationVertices[vertexIndex].Tangent);
            }

            if (normalizeBiTangents && destinationVertices[vertexIndex].BiTangent.LengthSquared() > float.Epsilon)
            {
                destinationVertices[vertexIndex].BiTangent = Vector3.Normalize(destinationVertices[vertexIndex].BiTangent);
            }
        }
    }
}