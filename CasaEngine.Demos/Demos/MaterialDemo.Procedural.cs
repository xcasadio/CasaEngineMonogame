using System;
using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>The meshes and textures the material demo builds in code, kept apart from the materials it shows.</summary>
public partial class MaterialDemo
{
    /// <summary>Builds and uploads a <see cref="StaticModelMesh"/> from a geometric primitive.</summary>
    private static StaticModelMesh BuildMesh(string name, GeometricPrimitive primitive, GraphicsDevice gd, bool useTangents = false)
    {
        var mesh = new StaticModelMesh { Name = name };

        if (useTangents)
        {
            mesh.SetData(BuildTangentVertices(primitive), primitive.Indices.ToArray());
        }
        else
        {
            mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        }

        mesh.Initialize(gd);
        return mesh;
    }

    /// <summary>Generates a procedural two-colour checkerboard <see cref="Texture2D"/>.</summary>
    private static Texture2D CreateCheckerTexture(GraphicsDevice gd, int size, Color a, Color b)
    {
        int cellSize = size / 8;
        var tex      = new Texture2D(gd, size, size);
        var data     = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = x / cellSize;
            int cy = y / cellSize;
            data[y * size + x] = (cx + cy) % 2 == 0 ? a : b;
        }

        tex.SetData(data);
        return tex;
    }

    private static Texture2D CreateAlphaCutoutTexture(GraphicsDevice gd, int size)
    {
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = x / (size - 1f) * 2f - 1f;
            float ny = y / (size - 1f) * 2f - 1f;
            float distance = MathF.Sqrt(nx * nx + ny * ny);

            float alpha = 0.0f;
            if (distance < 0.68f)
            {
                alpha = 1.0f;
            }
            else if (distance < 0.82f)
            {
                alpha = 1.0f - (distance - 0.68f) / 0.14f;
            }

            float shade = Math.Clamp((ny + 1.0f) * 0.5f, 0.0f, 1.0f);
            var fill = Color.Lerp(new Color(44, 92, 55), new Color(187, 222, 118), shade);
            data[y * size + x] = new Color(fill.R, fill.G, fill.B, (byte)(Math.Clamp(alpha, 0.0f, 1.0f) * 255f));
        }

        tex.SetData(data);
        return tex;
    }

    private static Texture2D CreateWaveNormalMap(GraphicsDevice gd, int size)
    {
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];
        float texel = 1.0f / size;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (size - 1f);
            float v = y / (size - 1f);
            float du = SampleWaveHeight(u + texel, v) - SampleWaveHeight(u - texel, v);
            float dv = SampleWaveHeight(u, v + texel) - SampleWaveHeight(u, v - texel);

            var normal = Vector3.Normalize(new Vector3(-du * 8.0f, -dv * 8.0f, 1.0f));
            data[y * size + x] = new Color(new Vector4(
                normal.X * 0.5f + 0.5f,
                normal.Y * 0.5f + 0.5f,
                normal.Z * 0.5f + 0.5f,
                1.0f));
        }

        tex.SetData(data);
        return tex;
    }

    private static float SampleWaveHeight(float u, float v)
    {
        float wrappedU = u - MathF.Floor(u);
        float wrappedV = v - MathF.Floor(v);
        float waveA = MathF.Sin(wrappedU * MathHelper.TwoPi * 4.0f);
        float waveB = MathF.Cos(wrappedV * MathHelper.TwoPi * 3.0f);
        float waveC = MathF.Sin((wrappedU + wrappedV) * MathHelper.TwoPi * 2.0f);
        return waveA * 0.30f + waveB * 0.25f + waveC * 0.20f;
    }

    private static VertexPositionNormalTextureTangent[] BuildTangentVertices(GeometricPrimitive primitive)
    {
        var vertices = primitive.Vertices;
        var indices = primitive.Indices;
        var tangents = new Vector3[vertices.Count];
        var bitangents = new Vector3[vertices.Count];

        for (int i = 0; i <= indices.Count - 3; i += 3)
        {
            int index0 = (int)indices[i];
            int index1 = (int)indices[i + 1];
            int index2 = (int)indices[i + 2];

            var vertex0 = vertices[index0];
            var vertex1 = vertices[index1];
            var vertex2 = vertices[index2];

            var edge1 = vertex1.Position - vertex0.Position;
            var edge2 = vertex2.Position - vertex0.Position;
            var deltaUv1 = vertex1.TextureCoordinate - vertex0.TextureCoordinate;
            var deltaUv2 = vertex2.TextureCoordinate - vertex0.TextureCoordinate;
            float determinant = deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y;

            if (MathF.Abs(determinant) < 1e-6f)
            {
                continue;
            }

            float inverseDeterminant = 1.0f / determinant;
            var tangent = (edge1 * deltaUv2.Y - edge2 * deltaUv1.Y) * inverseDeterminant;
            var bitangent = (edge2 * deltaUv1.X - edge1 * deltaUv2.X) * inverseDeterminant;

            tangents[index0] += tangent;
            tangents[index1] += tangent;
            tangents[index2] += tangent;
            bitangents[index0] += bitangent;
            bitangents[index1] += bitangent;
            bitangents[index2] += bitangent;
        }

        var tangentVertices = new VertexPositionNormalTextureTangent[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            var sourceVertex = vertices[i];
            var normal = Vector3.Normalize(sourceVertex.Normal);
            var tangent = tangents[i] - normal * Vector3.Dot(normal, tangents[i]);

            if (tangent.LengthSquared() < 1e-6f)
            {
                tangent = BuildFallbackTangent(normal);
            }
            else
            {
                tangent.Normalize();
            }

            float handedness = Vector3.Dot(Vector3.Cross(normal, tangent), bitangents[i]) < 0.0f ? -1.0f : 1.0f;
            tangentVertices[i] = new VertexPositionNormalTextureTangent(
                sourceVertex.Position,
                normal,
                sourceVertex.TextureCoordinate,
                new Vector4(tangent, handedness));
        }

        return tangentVertices;
    }

    private static Vector3 BuildFallbackTangent(Vector3 normal)
    {
        var referenceAxis = MathF.Abs(normal.Y) > 0.999f ? Vector3.Right : Vector3.Up;
        var tangent = Vector3.Cross(referenceAxis, normal);

        if (tangent.LengthSquared() < 1e-6f)
        {
            tangent = Vector3.Right;
        }
        else
        {
            tangent.Normalize();
        }

        return tangent;
    }
}
