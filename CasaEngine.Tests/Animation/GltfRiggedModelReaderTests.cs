using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class GltfRiggedModelReaderTests
{
    // 1x1 transparent PNG, used only so material.FindChannel("BaseColor")?.Texture?.Sampler
    // resolves to a real texture/sampler pair; pixel content is irrelevant to these tests.
    private const string TinyPngDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

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

            // Single top-level node ("MeshNode") becomes the model root directly; dummy bone only.
            Assert.Single(model.FlatListToAllNodes);
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
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void LoadAsset_GeneratesSmoothOutwardNormals_WhenNormalAttributeIsMissing()
    {
        // PSX-style exports often omit NORMAL entirely (matches the Aya-section18.glb case).
        string modelPath = CreateCubeGltfProbe(includeNormals: false);

        try
        {
            var reader = new GltfRiggedModelReader();
            var model = reader.LoadAsset(modelPath);

            var mesh = Assert.Single(model.Meshes);
            Assert.Equal(8, mesh.Vertices.Length);

            var centroid = Vector3.Zero; // the cube is centered on the origin.
            foreach (var vertex in mesh.Vertices)
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
    public void LoadAsset_PreservesExistingNormals_WhenNormalAttributeIsPresent()
    {
        // The authored normal (0,0,-1) deliberately disagrees with the geometric (0,0,1) normal
        // of this triangle, so a passing test proves the reader did not regenerate it.
        string modelPath = CreateTriangleWithExplicitNormalGltfProbe();

        try
        {
            var reader = new GltfRiggedModelReader();
            var model = reader.LoadAsset(modelPath);

            var mesh = Assert.Single(model.Meshes);
            Assert.All(mesh.Vertices, vertex => Assert.Equal(new Vector3(0f, 0f, -1f), vertex.Normal));
        }
        finally
        {
            DeleteProbeDirectory(modelPath);
        }
    }

    [Fact]
    public void LoadAsset_MapsMaterialFlags_DoubleSidedAlphaCutoffAndNearestFiltering()
    {
        string modelPath = CreateMaterialFlagsGltfProbe();

        try
        {
            var reader = new GltfRiggedModelReader();
            var model = reader.LoadAsset(modelPath);

            Assert.Equal(3, model.Meshes.Length);

            // Material 0: doubleSided + MASK cutoff 0.5 + NEAREST sampler (PSX-style material).
            var maskedDoubleSidedNearest = model.Meshes[0];
            Assert.True(maskedDoubleSidedNearest.IsDoubleSided);
            Assert.Equal(0.5f, maskedDoubleSidedNearest.AlphaCutoff);
            Assert.True(maskedDoubleSidedNearest.UseNearestFiltering);

            // Material 1: default OPAQUE, single-sided, no texture.
            var opaqueSingleSided = model.Meshes[1];
            Assert.False(opaqueSingleSided.IsDoubleSided);
            Assert.Equal(-1f, opaqueSingleSided.AlphaCutoff);
            Assert.False(opaqueSingleSided.UseNearestFiltering);

            // Material 2: OPAQUE with a LINEAR-filtered texture.
            var linearFiltered = model.Meshes[2];
            Assert.Equal(-1f, linearFiltered.AlphaCutoff);
            Assert.False(linearFiltered.UseNearestFiltering);
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

    /// <summary>
    /// A unit cube (8 shared vertices, 12 CCW/outward-winding glTF triangles) centered on the
    /// origin. Optionally omits NORMAL to probe the reader's generated-normal fallback.
    /// </summary>
    private static string CreateCubeGltfProbe(bool includeNormals)
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

        string normalAttribute = includeNormals ? ", \"NORMAL\": 2" : string.Empty;
        string normalAccessor = includeNormals
            ? """
            ,
            {
              "bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 8, "type": "VEC3"
            }
            """
            : string.Empty;

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0, "name": "CubeNode" } ],
          "meshes": [
            {
              "primitives": [
                { "attributes": { "POSITION": 0{{normalAttribute}} }, "indices": 1 }
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
            }{{normalAccessor}}
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

    private static string CreateTriangleWithExplicitNormalGltfProbe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        float[] positions = [0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f];
        ushort[] indices = [0, 1, 2];
        float[] normals = [0f, 0f, -1f, 0f, 0f, -1f, 0f, 0f, -1f];

        byte[] buffer = new byte[(positions.Length + normals.Length) * sizeof(float) + indices.Length * sizeof(ushort)];
        int offset = 0;
        foreach (var value in positions)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, sizeof(float));
            offset += sizeof(float);
        }
        int indicesOffset = offset;
        foreach (var value in indices)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, sizeof(ushort));
            offset += sizeof(ushort);
        }
        int normalsOffset = offset;
        foreach (var value in normals)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, sizeof(float));
            offset += sizeof(float);
        }

        string encodedBuffer = Convert.ToBase64String(buffer);
        string gltfPath = Path.Combine(directory, "triangle_with_normal.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0, "name": "MeshNode" } ],
          "meshes": [
            {
              "primitives": [
                { "attributes": { "POSITION": 0, "NORMAL": 2 }, "indices": 1 }
              ]
            }
          ],
          "buffers": [
            { "uri": "data:application/octet-stream;base64,{{encodedBuffer}}", "byteLength": {{buffer.Length}} }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 36, "target": 34962 },
            { "buffer": 0, "byteOffset": {{indicesOffset}}, "byteLength": 6, "target": 34963 },
            { "buffer": 0, "byteOffset": {{normalsOffset}}, "byteLength": 36, "target": 34962 }
          ],
          "accessors": [
            {
              "bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3",
              "max": [ 1.0, 1.0, 0.0 ], "min": [ 0.0, 0.0, 0.0 ]
            },
            {
              "bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR",
              "max": [ 2 ], "min": [ 0 ]
            },
            {
              "bufferView": 2, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3",
              "max": [ 0.0, 0.0, -1.0 ], "min": [ 0.0, 0.0, -1.0 ]
            }
          ]
        }
        """;

        File.WriteAllText(gltfPath, json);
        return gltfPath;
    }

    /// <summary>
    /// Three single-triangle meshes sharing one triangle buffer, each pointing at a different
    /// material so <see cref="Framework.Rendering.Models.RiggedModel.RiggedModelMesh.IsDoubleSided"/>,
    /// <c>AlphaCutoff</c> and <c>UseNearestFiltering</c> can be checked per combination.
    /// </summary>
    private static string CreateMaterialFlagsGltfProbe()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        byte[] buffer = CreateTriangleBuffer();
        string encodedBuffer = Convert.ToBase64String(buffer);
        string gltfPath = Path.Combine(directory, "material_flags.gltf");

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "scene": 0,
          "scenes": [ { "nodes": [ 0, 1, 2 ] } ],
          "nodes": [
            { "mesh": 0, "name": "MaskedDoubleSidedNearestNode" },
            { "mesh": 1, "name": "OpaqueSingleSidedNode" },
            { "mesh": 2, "name": "LinearFilteredNode" }
          ],
          "meshes": [
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 } ] },
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 1 } ] },
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 1, "material": 2 } ] }
          ],
          "materials": [
            {
              "name": "MaskedDoubleSidedNearest",
              "doubleSided": true,
              "alphaMode": "MASK",
              "alphaCutoff": 0.5,
              "pbrMetallicRoughness": { "baseColorTexture": { "index": 0 } }
            },
            {
              "name": "OpaqueSingleSided"
            },
            {
              "name": "LinearFiltered",
              "pbrMetallicRoughness": { "baseColorTexture": { "index": 1 } }
            }
          ],
          "textures": [
            { "sampler": 0, "source": 0 },
            { "sampler": 1, "source": 0 }
          ],
          "samplers": [
            { "magFilter": 9728, "minFilter": 9728 },
            { "magFilter": 9729, "minFilter": 9729 }
          ],
          "images": [
            { "uri": "{{TinyPngDataUri}}" }
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
}
