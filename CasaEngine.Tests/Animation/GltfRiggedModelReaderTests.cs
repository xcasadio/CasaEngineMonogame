using CasaEngine.Framework.Assets.Animations;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class GltfRiggedModelReaderTests
{
    [Fact]
    public void IsFileSupported_AcceptsGltfAndGlbOnly()
    {
        var reader = new GltfRiggedModelReader();

        Assert.True(reader.IsFileSupported("rig.gltf"));
        Assert.True(reader.IsFileSupported("rig.GLB"));
        Assert.False(reader.IsFileSupported("rig.fbx"));
    }

    [Fact]
    public void LoadAsset_BuildsRiggedModelAndRuntimeSkeletonFromUnskinnedGltf()
    {
        string modelPath = CreateMinimalGltfProbe();

        try
        {
            var reader = new GltfRiggedModelReader();
            var model = reader.LoadAsset(modelPath);

            Assert.NotNull(model);

            // Synthetic root + one mesh node => two flat nodes; dummy bone only.
            Assert.Equal(2, model.FlatListToAllNodes.Count);
            Assert.Single(model.FlatListToBoneNodes);

            // The runtime skeleton is built from the flat node list by InitializeRuntimeAnimation().
            Assert.NotNull(model.SkeletonDefinition);
            Assert.Equal(model.FlatListToAllNodes.Count, model.SkeletonDefinition.Count);
            Assert.Empty(model.AnimationClips);

            // One primitive => one mesh; unskinned vertices bind fully to bone 0.
            var mesh = Assert.Single(model.Meshes);
            Assert.Equal(3, mesh.Vertices.Length);
            Assert.Equal(1f, mesh.Vertices[0].BlendWeights.X);

            // Triangle winding reversed (A,C,B) to match the legacy importer.
            Assert.Equal(new int[] { 0, 2, 1 }, mesh.Indices);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string CreateMinimalGltfProbe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        byte[] buffer = CreateTriangleBuffer();
        string encodedBuffer = Convert.ToBase64String(buffer);
        string gltfPath = Path.Combine(directory, "rig.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0, "name": "MeshNode" } ],
          "meshes": [
            {
              "primitives": [
                { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 }
              ]
            }
          ],
          "materials": [
            {
              "name": "Probe",
              "pbrMetallicRoughness": {
                "baseColorFactor": [ 1.0, 1.0, 1.0, 1.0 ],
                "roughnessFactor": 0.5
              }
            }
          ],
          "buffers": [
            { "uri": "data:application/octet-stream;base64,{{encodedBuffer}}", "byteLength": 42 }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": 36, "byteLength": 6, "target": 34963 }
          ],
          "accessors": [
            {
              "bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3",
              "max": [ 1.0, 1.0, 0.0 ], "min": [ 0.0, 0.0, 0.0 ]
            },
            {
              "bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR",
              "max": [ 2 ], "min": [ 0 ]
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
