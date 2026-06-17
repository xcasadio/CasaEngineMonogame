using CasaEngine.Framework.Assets.Loaders;
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
