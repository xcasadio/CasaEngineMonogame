using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using System.Globalization;
using Xunit;

namespace CasaEngine.Tests.Graphics;

public class GltfStaticModelReaderTests
{
    [Fact]
    public void IsFileSupported_AcceptsGltfAndGlbOnly()
    {
        var reader = new GltfStaticModelReader();

        Assert.True(reader.IsFileSupported("model.gltf"));
        Assert.True(reader.IsFileSupported("model.GLB"));
        Assert.False(reader.IsFileSupported("model.fbx"));
        Assert.False(reader.IsFileSupported("model.obj"));
    }

    [Fact]
    public void ReadWithMetadata_BuildsGeometryHierarchyAndMaterial()
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: 0.25f);

        try
        {
            var reader = new GltfStaticModelReader();
            var result = reader.ReadWithMetadata(modelPath);

            var mesh = Assert.Single(result.Model.Meshes);
            Assert.Equal(3, mesh.GetVertices().Count);
            Assert.Equal(3, mesh.GetIndices().Count);
            Assert.Equal(0, mesh.MaterialIndex);

            Assert.NotNull(result.Model.RootNode);
            Assert.Equal(0, result.Model.RootNode.MeshIndex);

            var material = Assert.Single(result.Materials);
            // roughness 0.25 -> 2/(0.25^2) - 2 = 30
            Assert.InRange(material.SpecularPower, 29.9f, 30.1f);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_ReversesTriangleWindingToMatchLegacyImporter()
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: 0.5f);

        try
        {
            var reader = new GltfStaticModelReader();
            var result = reader.ReadWithMetadata(modelPath);

            var indices = Assert.Single(result.Model.Meshes).GetIndices();
            // Source triangle [0,1,2] is emitted reversed (A,C,B) -> [0,2,1].
            Assert.Equal(new uint[] { 0, 2, 1 }, indices.ToArray());
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_GeneratesSmoothOutwardNormals_WhenNormalAttributeIsMissing()
    {
        // PSX-style exports often omit NORMAL entirely (matches the Aya-section18.glb case).
        string modelPath = CreateCubeGltfProbe();

        try
        {
            var reader = new GltfStaticModelReader();
            var result = reader.ReadWithMetadata(modelPath);

            var mesh = Assert.Single(result.Model.Meshes);
            var vertices = mesh.GetVertices();
            Assert.Equal(8, vertices.Count);

            var centroid = Vector3.Zero; // the cube is centered on the origin.
            foreach (var vertex in vertices)
            {
                float length = vertex.Normal.Length();
                Assert.InRange(length, 0.99f, 1.01f);

                float outwardDot = Vector3.Dot(vertex.Normal, vertex.Position - centroid);
                Assert.True(outwardDot > 0f, $"normal {vertex.Normal} should point away from the cube centroid at {vertex.Position}");
            }
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_ReadsTangentAttribute_AndKeepsGeometryAndWinding()
    {
        string modelPath = CreateTangentGltfProbe(withTangents: true);

        try
        {
            var reader = new GltfStaticModelReader();
            var result = reader.ReadWithMetadata(modelPath);

            var mesh = Assert.Single(result.Model.Meshes);
            Assert.True(mesh.HasTangents);
            Assert.Equal(3, mesh.GetVertices().Count);
            Assert.Equal(new uint[] { 0, 2, 1 }, mesh.GetIndices().ToArray());

            var vertices = mesh.GetVertices();
            Assert.Equal(new Vector3(1f, 0f, 0f), vertices[1].Position);
            Assert.Equal(new Vector3(0f, 0f, 1f), vertices[1].Normal);
            Assert.Equal(new Vector2(1f, 0f), vertices[1].TextureCoordinate);

            // Vertex 0 stores (2, 0, 0, -1): xyz is normalized, w is kept as authored.
            // Vertices 1 and 2 store unit tangents (0, 1, 0, +1) and (1, 0, 0, +1).
            var tangents = ReadTangents(mesh);
            Assert.Equal(3, tangents.Length);
            Assert.Equal(new Vector4(1f, 0f, 0f, -1f), tangents[0]);
            Assert.Equal(new Vector4(0f, 1f, 0f, 1f), tangents[1]);
            Assert.Equal(new Vector4(1f, 0f, 0f, 1f), tangents[2]);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_HasNoTangents_WhenTangentAttributeIsMissing()
    {
        string modelPath = CreateTangentGltfProbe(withTangents: false);

        try
        {
            var reader = new GltfStaticModelReader();
            var result = reader.ReadWithMetadata(modelPath);

            var mesh = Assert.Single(result.Model.Meshes);
            Assert.False(mesh.HasTangents);
            Assert.Equal(3, mesh.GetVertices().Count);
            Assert.Equal(new uint[] { 0, 2, 1 }, mesh.GetIndices().ToArray());
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    private static Vector4[] ReadTangents(StaticModelMesh mesh)
    {
        // The tangent vertices are only exposed through the GPU vertex buffer (needs a GraphicsDevice),
        // so read the stored array directly.
        var field = typeof(StaticModelMesh)
            .GetField("_tangentVertices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var vertices = (VertexPositionNormalTextureTangent[])field.GetValue(mesh)!;
        return vertices.Select(v => v.Tangent).ToArray();
    }

    private static string CreateTangentGltfProbe(bool withTangents)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        float[] positions = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];
        float[] normals = [0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f];
        float[] uvs = [0f, 0f, 1f, 0f, 0f, 1f];
        float[] tangents = [2f, 0f, 0f, -1f, 0f, 1f, 0f, 1f, 1f, 0f, 0f, 1f];
        ushort[] indices = [0, 1, 2];

        var bytes = new List<byte>();
        foreach (float value in positions.Concat(normals).Concat(uvs).Concat(withTangents ? tangents : []))
        {
            bytes.AddRange(BitConverter.GetBytes(value));
        }

        int floatBytes = bytes.Count;
        foreach (ushort index in indices)
        {
            bytes.AddRange(BitConverter.GetBytes(index));
        }

        int tangentOffset = (positions.Length + normals.Length + uvs.Length) * sizeof(float);
        string tangentView = withTangents
            ? $$""", { "buffer": 0, "byteOffset": {{tangentOffset}}, "byteLength": {{tangents.Length * sizeof(float)}}, "target": 34962 }"""
            : string.Empty;
        string tangentAccessor = withTangents
            ? """, { "bufferView": 4, "componentType": 5126, "count": 3, "type": "VEC4" }"""
            : string.Empty;
        string tangentAttribute = withTangents ? """, "TANGENT": 4""" : string.Empty;

        // The index view is declared before the optional tangent view so the accessor numbers stay fixed.
        string encodedBuffer = Convert.ToBase64String(bytes.ToArray());
        string gltfPath = Path.Combine(directory, "tangent-probe.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0 } ],
          "meshes": [
            {
              "primitives": [
                { "attributes": { "POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2{{tangentAttribute}} }, "indices": 3 }
              ]
            }
          ],
          "buffers": [
            { "uri": "data:application/octet-stream;base64,{{encodedBuffer}}", "byteLength": {{bytes.Count}} }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 72, "byteLength": 24, "target": 34962 },
            { "buffer": 0, "byteOffset": {{floatBytes}}, "byteLength": 6, "target": 34963 }{{tangentView}}
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "max": [ 1.0, 1.0, 0.0 ], "min": [ 0.0, 0.0, 0.0 ] },
            { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
            { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
            { "bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR", "max": [ 2 ], "min": [ 0 ] }{{tangentAccessor}}
          ]
        }
        """;

        File.WriteAllText(gltfPath, json);
        return gltfPath;
    }

    private static string CreateCubeGltfProbe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        byte[] positionBytes = CreateCubePositionBytes();
        byte[] indexBytes = CreateCubeIndexBytes();
        byte[] buffer = new byte[positionBytes.Length + indexBytes.Length];
        Buffer.BlockCopy(positionBytes, 0, buffer, 0, positionBytes.Length);
        Buffer.BlockCopy(indexBytes, 0, buffer, positionBytes.Length, indexBytes.Length);

        string encodedBuffer = Convert.ToBase64String(buffer);
        string gltfPath = Path.Combine(directory, "cube.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0, "name": "CubeNode" } ],
          "meshes": [
            {
              "primitives": [
                { "attributes": { "POSITION": 0 }, "indices": 1 }
              ]
            }
          ],
          "buffers": [
            { "uri": "data:application/octet-stream;base64,{{encodedBuffer}}", "byteLength": {{buffer.Length}} }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": {{positionBytes.Length}}, "target": 34962 },
            { "buffer": 0, "byteOffset": {{positionBytes.Length}}, "byteLength": {{indexBytes.Length}}, "target": 34963 }
          ],
          "accessors": [
            {
              "bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 8, "type": "VEC3",
              "max": [ 1.0, 1.0, 1.0 ], "min": [ -1.0, -1.0, -1.0 ]
            },
            {
              "bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 36, "type": "SCALAR",
              "max": [ 7 ], "min": [ 0 ]
            }
          ]
        }
        """;

        File.WriteAllText(gltfPath, json);
        return gltfPath;
    }

    private static byte[] CreateCubePositionBytes()
    {
        float[] positions =
        [
            -1f, -1f, -1f,
             1f, -1f, -1f,
             1f,  1f, -1f,
            -1f,  1f, -1f,
            -1f, -1f,  1f,
             1f, -1f,  1f,
             1f,  1f,  1f,
            -1f,  1f,  1f,
        ];

        byte[] buffer = new byte[positions.Length * sizeof(float)];
        for (int i = 0; i < positions.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(positions[i]), 0, buffer, i * sizeof(float), sizeof(float));
        }

        return buffer;
    }

    private static byte[] CreateCubeIndexBytes()
    {
        // Outward-facing (CCW as seen from outside), matching glTF's default front-face winding.
        ushort[] indices =
        [
            0, 3, 2, 0, 2, 1, // -Z
            4, 5, 6, 4, 6, 7, // +Z
            0, 7, 3, 0, 4, 7, // -X
            1, 2, 6, 1, 6, 5, // +X
            0, 1, 5, 0, 5, 4, // -Y
            3, 6, 2, 3, 7, 6, // +Y
        ];

        byte[] buffer = new byte[indices.Length * sizeof(ushort)];
        for (int i = 0; i < indices.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(indices[i]), 0, buffer, i * sizeof(ushort), sizeof(ushort));
        }

        return buffer;
    }

    [Fact]
    public void ReadWithMetadata_DecomposesANodeWrittenAsAMatrix()
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: 0.25f);

        try
        {
            string json = File.ReadAllText(modelPath);
            string matrixNode = "\"nodes\": [ { \"mesh\": 0, \"matrix\": [ 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 1, 2, 3, 1 ] } ]";
            string withMatrix = json.Replace("\"nodes\": [ { \"mesh\": 0 } ]", matrixNode);
            Assert.NotEqual(json, withMatrix);
            File.WriteAllText(modelPath, withMatrix);

            var result = new GltfStaticModelReader().ReadWithMetadata(modelPath);

            Assert.NotNull(result.Model.RootNode);
            Assert.Equal(new Microsoft.Xna.Framework.Vector3(1f, 2f, 3f), result.Model.RootNode.Position);
            Assert.Equal(new Microsoft.Xna.Framework.Vector3(2f, 2f, 2f), result.Model.RootNode.Scale);
            Assert.Equal(Microsoft.Xna.Framework.Quaternion.Identity, result.Model.RootNode.Rotation);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_ReadsEveryPbrChannelOfAMaterial()
    {
        string modelPath = CreatePbrGltfProbe();

        try
        {
            var result = new GltfStaticModelReader().ReadWithMetadata(modelPath);
            string directory = Path.GetDirectoryName(modelPath)!;

            Assert.Equal(6, result.Materials.Count);
            var opaque = result.Materials[0];

            Assert.Equal(0.3f, opaque.MetallicFactor, 4);
            Assert.Equal(0.6f, opaque.RoughnessFactor, 4);
            Assert.Equal(0.7f, opaque.NormalScale, 4);
            Assert.Equal(0.4f, opaque.OcclusionStrength, 4);
            Assert.Equal(StaticModelAlphaMode.Opaque, opaque.AlphaMode);
            Assert.False(opaque.DoubleSided);

            Assert.Equal(Path.Combine(directory, "base.png"), opaque.DiffuseTextureFilePath);
            Assert.Equal(Path.Combine(directory, "normal.png"), opaque.NormalTextureFilePath);
            Assert.Equal(Path.Combine(directory, "mr.png"), opaque.MetallicRoughnessTextureFilePath);
            Assert.Equal(Path.Combine(directory, "occlusion.png"), opaque.OcclusionTextureFilePath);
            Assert.Equal(Path.Combine(directory, "emissive.png"), opaque.EmissiveTextureFilePath);

            // The fields that were already read keep their values: 0.5 -> byte 127, emissive factor as authored.
            Assert.Equal(127, opaque.DiffuseColor.R);
            Assert.Equal(0.5f, opaque.EmissiveColor.X, 4);
            Assert.Equal(0.5f, opaque.EmissiveColor.Z, 4);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_ReadsAlphaModeCutoffAndDoubleSided()
    {
        string modelPath = CreatePbrGltfProbe();

        try
        {
            var result = new GltfStaticModelReader().ReadWithMetadata(modelPath);

            var masked = result.Materials[1];
            Assert.Equal(StaticModelAlphaMode.Mask, masked.AlphaMode);
            Assert.Equal(0.35f, masked.AlphaCutoff, 4);
            Assert.True(masked.DoubleSided);
            Assert.Equal(0.9f, masked.MetallicFactor, 4);
            Assert.Equal(0.1f, masked.RoughnessFactor, 4);

            var blended = result.Materials[2];
            Assert.Equal(StaticModelAlphaMode.Blend, blended.AlphaMode);
            Assert.False(blended.DoubleSided);

            var opaqueDoubleSided = result.Materials[3];
            Assert.Equal(StaticModelAlphaMode.Opaque, opaqueDoubleSided.AlphaMode);
            Assert.True(opaqueDoubleSided.DoubleSided);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_UsesTheGltfDefaultsAndNoTexturePathForAPlainMaterial()
    {
        string modelPath = CreatePbrGltfProbe();

        try
        {
            var plain = new GltfStaticModelReader().ReadWithMetadata(modelPath).Materials[4];

            Assert.Equal(1.0f, plain.MetallicFactor, 4);
            Assert.Equal(1.0f, plain.RoughnessFactor, 4);
            Assert.Equal(1.0f, plain.NormalScale, 4);
            Assert.Equal(1.0f, plain.OcclusionStrength, 4);
            Assert.Equal(StaticModelAlphaMode.Opaque, plain.AlphaMode);
            Assert.Equal(0.5f, plain.AlphaCutoff, 4);
            Assert.False(plain.DoubleSided);
            Assert.Null(plain.MetallicRoughnessTextureFilePath);
            Assert.Null(plain.OcclusionTextureFilePath);
            Assert.Null(plain.EmissiveTextureFilePath);
            Assert.Null(plain.NormalTextureFilePath);
            Assert.Null(plain.DiffuseTextureFilePath);

            var unlit = new GltfStaticModelReader().ReadWithMetadata(modelPath).Materials[5];
            Assert.Equal(0.0f, unlit.MetallicFactor, 4);
            Assert.Equal(1.0f, unlit.RoughnessFactor, 4);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Theory]
    [InlineData(0.0f, 128.0f)]
    [InlineData(0.25f, 30.0f)]
    [InlineData(0.6f, 3.5556f)]
    [InlineData(1.0f, 0.0f)]
    public void ReadWithMetadata_KeepsTheRoughnessToSpecularPowerConversionNextToTheNewFields(float roughness, float expectedPower)
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: roughness);

        try
        {
            var material = Assert.Single(new GltfStaticModelReader().ReadWithMetadata(modelPath).Materials);

            Assert.Equal(expectedPower, material.SpecularPower, 2);
            Assert.Equal(roughness, material.RoughnessFactor, 4);
            Assert.Equal(0.8f, material.MetallicFactor, 4);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    /// <summary>
    /// Proof of T2.2 (plan clear coat, step 1): SharpGLTF 1.0.6 can write the KHR_materials_clearcoat extension through its own API
    /// (<c>InitializePBRMetallicRoughness("ClearCoat")</c> then the <c>ClearCoat</c> channel, factor <c>ClearCoatFactor</c>, and the
    /// <c>ClearCoatRoughness</c> channel, factor <c>RoughnessFactor</c>) and read it back; the reader
    /// of the engine then sees the same factors.
    /// </summary>
    [Fact]
    public void SharpGltf_WritesAndReadsTheClearCoatChannel_ThroughItsApi()
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: 0.5f);

        try
        {
            string clearCoatPath = WriteClearCoatVariant(modelPath, clearCoatFactor: 0.8f, clearCoatRoughnessFactor: 0.2f);

            var reloaded = SharpGLTF.Schema2.ModelRoot.Load(clearCoatPath);
            var channel = reloaded.LogicalMaterials[0].FindChannel("ClearCoat");
            var roughnessChannel = reloaded.LogicalMaterials[0].FindChannel("ClearCoatRoughness");

            Assert.NotNull(channel);
            Assert.Equal(0.8f, channel.Value.GetFactor("ClearCoatFactor"), 4);
            Assert.NotNull(roughnessChannel);
            Assert.Equal(0.2f, roughnessChannel.Value.GetFactor("RoughnessFactor"), 4);
            Assert.Contains("KHR_materials_clearcoat", File.ReadAllText(clearCoatPath));
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_ReadsTheClearCoatFactorAndRoughness()
    {
        string modelPath = CreateMinimalGltfProbe(roughnessFactor: 0.5f);

        try
        {
            string clearCoatPath = WriteClearCoatVariant(modelPath, clearCoatFactor: 0.8f, clearCoatRoughnessFactor: 0.2f);

            var material = Assert.Single(new GltfStaticModelReader().ReadWithMetadata(clearCoatPath).Materials);

            Assert.Equal(0.8f, material.ClearCoatFactor, 4);
            Assert.Equal(0.2f, material.ClearCoatRoughnessFactor, 4);
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void ReadWithMetadata_LeavesTheClearCoatAtZeroForMaterialsWithoutTheExtension()
    {
        string modelPath = CreatePbrGltfProbe();

        try
        {
            var result = new GltfStaticModelReader().ReadWithMetadata(modelPath);

            Assert.Equal(6, result.Materials.Count);
            foreach (var material in result.Materials)
            {
                Assert.Equal(0.0f, material.ClearCoatFactor);
                Assert.Equal(0.0f, material.ClearCoatRoughnessFactor);
            }
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    private static string WriteClearCoatVariant(string sourcePath, float clearCoatFactor, float clearCoatRoughnessFactor)
    {
        var model = SharpGLTF.Schema2.ModelRoot.Load(sourcePath);
        var material = model.LogicalMaterials[0];
        material.InitializePBRMetallicRoughness("ClearCoat");
        var channel = material.FindChannel("ClearCoat")!.Value;
        channel.SetFactor("ClearCoatFactor", clearCoatFactor);
        material.FindChannel("ClearCoatRoughness")!.Value.SetFactor("RoughnessFactor", clearCoatRoughnessFactor);

        string path = Path.Combine(Path.GetDirectoryName(sourcePath)!, "clearcoat.gltf");
        model.SaveGLTF(path);
        return path;
    }

    /// <summary>
    /// A glTF with six materials: a fully textured opaque one with non-default factors, a masked double-sided one,
    /// a blended one, an opaque double-sided one a plain one that relies on every glTF default and an unlit one.
    /// The five external images exist on disk as 1x1 PNG files.
    /// </summary>
    private static string CreatePbrGltfProbe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");
        string[] imageNames = ["base.png", "normal.png", "mr.png", "occlusion.png", "emissive.png"];
        foreach (string imageName in imageNames)
        {
            File.WriteAllBytes(Path.Combine(directory, imageName), png);
        }

        string encodedBuffer = Convert.ToBase64String(CreateTriangleBuffer());
        string gltfPath = Path.Combine(directory, "pbr.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": [ "KHR_materials_unlit" ],
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0 } ],
          "meshes": [
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 } ] }
          ],
          "images": [
            { "uri": "base.png" }, { "uri": "normal.png" }, { "uri": "mr.png" }, { "uri": "occlusion.png" }, { "uri": "emissive.png" }
          ],
          "textures": [
            { "source": 0 }, { "source": 1 }, { "source": 2 }, { "source": 3 }, { "source": 4 }
          ],
          "materials": [
            {
              "name": "Opaque",
              "pbrMetallicRoughness": {
                "baseColorFactor": [ 0.5, 0.5, 0.5, 1.0 ],
                "baseColorTexture": { "index": 0 },
                "metallicFactor": 0.3,
                "roughnessFactor": 0.6,
                "metallicRoughnessTexture": { "index": 2 }
              },
              "normalTexture": { "index": 1, "scale": 0.7 },
              "occlusionTexture": { "index": 3, "strength": 0.4 },
              "emissiveTexture": { "index": 4 },
              "emissiveFactor": [ 0.5, 0.5, 0.5 ],
              "alphaMode": "OPAQUE"
            },
            {
              "name": "Masked",
              "pbrMetallicRoughness": { "metallicFactor": 0.9, "roughnessFactor": 0.1 },
              "alphaMode": "MASK",
              "alphaCutoff": 0.35,
              "doubleSided": true
            },
            {
              "name": "Blended",
              "pbrMetallicRoughness": { "baseColorFactor": [ 1.0, 1.0, 1.0, 0.5 ] },
              "alphaMode": "BLEND"
            },
            { "name": "OpaqueDoubleSided", "doubleSided": true },
            { "name": "Plain" },
            { "name": "Unlit", "extensions": { "KHR_materials_unlit": {} } }
          ],
          "buffers": [
            { "uri": "data:application/octet-stream;base64,{{encodedBuffer}}", "byteLength": 42 }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            { "bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3", "max": [ 1.0, 1.0, 0.0 ], "min": [ 0.0, 0.0, 0.0 ] },
            { "bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR", "max": [ 2 ], "min": [ 0 ] }
          ]
        }
        """;

        File.WriteAllText(gltfPath, json);
        return gltfPath;
    }

    private static void DeleteProbeDirectory(string modelPath)
    {
        string? directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateMinimalGltfProbe(float roughnessFactor)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        byte[] buffer = CreateTriangleBuffer();
        string encodedBuffer = Convert.ToBase64String(buffer);
        string roughnessText = roughnessFactor.ToString(CultureInfo.InvariantCulture);
        string gltfPath = Path.Combine(directory, "probe.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0 } ],
          "meshes": [
            {
              "primitives": [
                {
                  "attributes": { "POSITION": 0 },
                  "indices": 1,
                  "material": 0
                }
              ]
            }
          ],
          "materials": [
            {
              "name": "Probe",
              "pbrMetallicRoughness": {
                "baseColorFactor": [ 1.0, 1.0, 1.0, 1.0 ],
                "metallicFactor": 0.8,
                "roughnessFactor": {{roughnessText}}
              }
            }
          ],
          "buffers": [
            {
              "uri": "data:application/octet-stream;base64,{{encodedBuffer}}",
              "byteLength": 42
            }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            {
              "bufferView": 0,
              "byteOffset": 0,
              "componentType": 5126,
              "count": 3,
              "type": "VEC3",
              "max": [ 1.0, 1.0, 0.0 ],
              "min": [ 0.0, 0.0, 0.0 ]
            },
            {
              "bufferView": 1,
              "byteOffset": 0,
              "componentType": 5123,
              "count": 3,
              "type": "SCALAR",
              "max": [ 2 ],
              "min": [ 0 ]
            }
          ]
        }
        """;

        File.WriteAllText(gltfPath, json);
        return gltfPath;
    }

    private static byte[] CreateTriangleBuffer()
    {
        byte[] buffer = new byte[42];
        float[] positions = [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f];
        ushort[] indices = [0, 1, 2];

        for (int i = 0; i < positions.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(positions[i]), 0, buffer, i * sizeof(float), sizeof(float));
        }

        for (int i = 0; i < indices.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(indices[i]), 0, buffer, 36 + (i * sizeof(ushort)), sizeof(ushort));
        }

        return buffer;
    }
}
