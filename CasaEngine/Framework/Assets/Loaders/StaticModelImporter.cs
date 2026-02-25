using Assimp;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Imports a 3-D file (FBX, OBJ, GLTF, …) as a <see cref="StaticModel"/> asset
/// using AssimpNet.  Only the geometry, hierarchy and diffuse texture paths are
/// preserved.  No skeleton or animation data is read.
/// </summary>
public class StaticModelImporter
{
    private readonly AssimpContext _assimpContext = new();

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    public bool IsFileSupported(string fileName) =>
        _assimpContext.GetSupportedImportFormats().Contains(
            Path.GetExtension(fileName).ToLower());

    /// <summary>
    /// Import <paramref name="filePath"/> and return a populated
    /// <see cref="StaticModel"/>.  Call
    /// <see cref="StaticModel.Initialize"/> afterwards to upload GPU buffers.
    /// </summary>
    public StaticModel Import(string filePath)
    {
        Scene scene;
        try
        {
            scene = _assimpContext.ImportFile(filePath,
                PostProcessSteps.Triangulate
                | PostProcessSteps.FlipUVs
                | PostProcessSteps.JoinIdenticalVertices
                | PostProcessSteps.GenerateSmoothNormals
                | PostProcessSteps.FlipWindingOrder
                | PostProcessSteps.GlobalScale);
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return new StaticModel();
        }

        var model = new StaticModel();
        model.Name = Path.GetFileNameWithoutExtension(filePath);

        // Build flat mesh list
        for (int i = 0; i < scene.Meshes.Count; i++)
        {
            var assimpMesh = scene.Meshes[i];
            var modelMesh = BuildMesh(assimpMesh, i, scene, filePath);
            model.Meshes.Add(modelMesh);
        }

        // Build node hierarchy
        if (scene.RootNode != null)
        {
            model.RootNode = BuildNode(scene.RootNode);
        }

        return model;
    }

    /// <summary>
    /// Returns all diffuse texture file paths embedded in the file,
    /// useful to trigger texture import alongside the model.
    /// </summary>
    public IReadOnlyList<string> GetTextureFilePaths(string filePath)
    {
        Scene scene;
        try
        {
            scene = _assimpContext.ImportFile(filePath, PostProcessSteps.None);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var paths = new List<string>();
        foreach (var material in scene.Materials)
        {
            foreach (var slot in material.GetAllMaterialTextures())
            {
                var texturePath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileName(slot.FilePath));
                if (File.Exists(texturePath) && !paths.Contains(texturePath))
                {
                    paths.Add(texturePath);
                }
            }
        }
        return paths;
    }

    // -----------------------------------------------------------------------
    //  Private helpers
    // -----------------------------------------------------------------------

    private static StaticModelMesh BuildMesh(Mesh assimpMesh, int meshIndex, Scene scene, string filePath)
    {
        var modelMesh = new StaticModelMesh();
        modelMesh.Name = assimpMesh.Name;
        modelMesh.MaterialIndex = assimpMesh.MaterialIndex;

        // --- Vertices ---
        var vertices = new VertexPositionNormalTexture[assimpMesh.VertexCount];

        for (int k = 0; k < assimpMesh.Vertices.Count; k++)
        {
            var p = assimpMesh.Vertices[k];
            vertices[k].Position = new Vector3(p.X, p.Y, p.Z);
        }

        if (assimpMesh.HasNormals)
        {
            for (int k = 0; k < assimpMesh.Normals.Count; k++)
            {
                var n = assimpMesh.Normals[k];
                vertices[k].Normal = new Vector3(n.X, n.Y, n.Z);
            }
        }

        if (assimpMesh.HasTextureCoords(0))
        {
            var uvChannel = assimpMesh.TextureCoordinateChannels[0];
            for (int k = 0; k < uvChannel.Count; k++)
            {
                vertices[k].TextureCoordinate = new Microsoft.Xna.Framework.Vector2(uvChannel[k].X, uvChannel[k].Y);
            }
        }

        // --- Indices ---
        var indices = new uint[assimpMesh.FaceCount * 3];
        int idx = 0;
        foreach (var face in assimpMesh.Faces)
        {
            foreach (var index in face.Indices)
            {
                indices[idx++] = (uint)index;
            }
        }

        modelMesh.SetData(vertices, indices);

        // --- Diffuse texture path ---
        if (assimpMesh.MaterialIndex < scene.Materials.Count)
        {
            var material = scene.Materials[assimpMesh.MaterialIndex];
            foreach (var slot in material.GetAllMaterialTextures())
            {
                if (slot.TextureType == TextureType.Diffuse)
                {
                    var texturePath = Path.Combine(
                        Path.GetDirectoryName(filePath)!,
                        Path.GetFileName(slot.FilePath));
                    if (File.Exists(texturePath))
                    {
                        modelMesh.DiffuseTextureFilePath = texturePath;
                    }
                    break;
                }
            }
        }

        return modelMesh;
    }

    private static StaticModelNode BuildNode(Node assimpNode)
    {
        var node = new StaticModelNode();
        node.Name = assimpNode.Name;

        // Decompose the local transform matrix into TRS
        var localMatrix = assimpNode.Transform.ToMonoGameTransposed();
        if (localMatrix.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 position))
        {
            node.Position = position;
            node.Rotation = rotation;
            node.Scale = scale;
        }

        // Assign meshIndex if this node directly owns exactly one mesh;
        // for multi-mesh nodes we create one child per extra mesh index.
        if (assimpNode.MeshIndices.Count == 1)
        {
            node.MeshIndex = assimpNode.MeshIndices[0];
        }
        else if (assimpNode.MeshIndices.Count > 1)
        {
            // First mesh on this node itself
            node.MeshIndex = assimpNode.MeshIndices[0];

            // Extra meshes become synthetic children
            for (int i = 1; i < assimpNode.MeshIndices.Count; i++)
            {
                var extra = new StaticModelNode();
                extra.Name = assimpNode.Name + "_mesh" + i;
                extra.MeshIndex = assimpNode.MeshIndices[i];
                node.Children.Add(extra);
            }
        }

        // Recurse into children
        foreach (var child in assimpNode.Children)
        {
            node.Children.Add(BuildNode(child));
        }

        return node;
    }
}
