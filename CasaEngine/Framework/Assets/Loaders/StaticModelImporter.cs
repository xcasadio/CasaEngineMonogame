using Assimp;
using CasaEngine.Core.Log;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.RegularExpressions;
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
    private static readonly Regex MaterialPrefixRegex = new(@"^Material_+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericPrefixRegex = new(@"^\d+_+", RegexOptions.Compiled);
    private static readonly Regex MaterialSuffixRegex = new(@"Sub\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    public bool IsFileSupported(string fileName) =>
        _assimpContext.GetSupportedImportFormats().Contains(
            Path.GetExtension(fileName).ToLower());

    public StaticModelImportResult ImportWithMetadata(string filePath)
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
            return new StaticModelImportResult(new StaticModel(), Array.Empty<StaticModelImportedMaterial>());
        }

        var model = new StaticModel
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
        };

        var importedMaterials = BuildMaterials(scene, filePath);

        for (int i = 0; i < scene.Meshes.Count; i++)
        {
            var assimpMesh = scene.Meshes[i];
            var modelMesh = BuildMesh(assimpMesh, importedMaterials);
            model.Meshes.Add(modelMesh);
        }

        if (scene.RootNode != null)
        {
            model.RootNode = BuildNode(scene.RootNode, model, importedMaterials);
        }

        StaticModelMaterialSlots.EnsureMetadata(model);

        return new StaticModelImportResult(model, importedMaterials);
    }

    /// <summary>
    /// Import <paramref name="filePath"/> and return a populated
    /// <see cref="StaticModel"/>.  Call
    /// <see cref="StaticModel.Initialize"/> afterwards to upload GPU buffers.
    /// </summary>
    public StaticModel Import(string filePath)
    {
        return ImportWithMetadata(filePath).Model;
    }

    /// <summary>
    /// Returns all diffuse texture file paths embedded in the file,
    /// useful to trigger texture import alongside the model.
    /// </summary>
    public IReadOnlyList<string> GetTextureFilePaths(string filePath)
    {
        var paths = new List<string>();
        Scene scene;
        try
        {
            scene = ImportScene(filePath, PostProcessSteps.None);
        }
        catch
        {
            return Array.Empty<string>();
        }

        foreach (var material in BuildMaterials(scene, filePath))
        {
            if (!string.IsNullOrWhiteSpace(material.DiffuseTextureFilePath) && !paths.Contains(material.DiffuseTextureFilePath))
            {
                paths.Add(material.DiffuseTextureFilePath);
            }

            if (!string.IsNullOrWhiteSpace(material.NormalTextureFilePath) && !paths.Contains(material.NormalTextureFilePath))
            {
                paths.Add(material.NormalTextureFilePath);
            }
        }

        return paths;
    }

    // -----------------------------------------------------------------------
    //  Private helpers
    // -----------------------------------------------------------------------

    private Scene ImportScene(string filePath, PostProcessSteps postProcessSteps)
    {
        return _assimpContext.ImportFile(filePath, postProcessSteps);
    }

    private static List<StaticModelImportedMaterial> BuildMaterials(Scene? scene, string filePath)
    {
        var result = new List<StaticModelImportedMaterial>();
        if (scene == null)
        {
            return result;
        }

        for (int i = 0; i < scene.Materials.Count; i++)
        {
            var material = scene.Materials[i];
            result.Add(new StaticModelImportedMaterial
            {
                MaterialIndex = i,
                Name = material.Name ?? string.Empty,
                DisplayName = BuildMaterialDisplayName(material, i, filePath),
                DiffuseTextureFilePath = ResolveTextureFilePath(material, filePath, TextureType.Diffuse),
                NormalTextureFilePath = ResolveNormalTextureFilePath(material, filePath),
                DiffuseColor = material.HasColorDiffuse
                    ? ToXnaColor(material.ColorDiffuse)
                    : Color.White,
                EmissiveColor = material.HasColorEmissive
                    ? new Vector3(material.ColorEmissive.R, material.ColorEmissive.G, material.ColorEmissive.B)
                    : Vector3.Zero,
                SpecularColor = material.HasColorSpecular
                    ? new Vector3(material.ColorSpecular.R, material.ColorSpecular.G, material.ColorSpecular.B)
                    : new Vector3(0.5f),
                SpecularPower = material.HasShininess
                    ? material.Shininess
                    : 16.0f,
            });
        }

        return result;
    }

    private static StaticModelMesh BuildMesh(Mesh assimpMesh, IReadOnlyList<StaticModelImportedMaterial> importedMaterials)
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
                vertices[k].TextureCoordinate = new Vector2(uvChannel[k].X, uvChannel[k].Y);
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

        if (assimpMesh.MaterialIndex >= 0 && assimpMesh.MaterialIndex < importedMaterials.Count)
        {
            modelMesh.DiffuseTextureFilePath = importedMaterials[assimpMesh.MaterialIndex].DiffuseTextureFilePath;
        }

        return modelMesh;
    }

    private static StaticModelNode BuildNode(Node assimpNode, StaticModel model, IReadOnlyList<StaticModelImportedMaterial> importedMaterials)
    {
        var node = new StaticModelNode();
        node.Name = assimpNode.Name;
        var usedSlotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            ApplyReadableMeshName(model, node.MeshIndex, assimpNode.Name, usedSlotNames, importedMaterials, 0);
        }
        else if (assimpNode.MeshIndices.Count > 1)
        {
            // First mesh on this node itself
            node.MeshIndex = assimpNode.MeshIndices[0];
            ApplyReadableMeshName(model, node.MeshIndex, assimpNode.Name, usedSlotNames, importedMaterials, 0);

            // Extra meshes become synthetic children
            for (int i = 1; i < assimpNode.MeshIndices.Count; i++)
            {
                int meshIndex = assimpNode.MeshIndices[i];
                string slotName = ApplyReadableMeshName(model, meshIndex, assimpNode.Name, usedSlotNames, importedMaterials, i);
                var extra = new StaticModelNode();
                extra.Name = slotName;
                extra.MeshIndex = meshIndex;
                node.Children.Add(extra);
            }
        }

        // Recurse into children
        foreach (var child in assimpNode.Children)
        {
            node.Children.Add(BuildNode(child, model, importedMaterials));
        }

        return node;
    }

    private static string ApplyReadableMeshName(
        StaticModel model,
        int meshIndex,
        string nodeName,
        HashSet<string> usedSlotNames,
        IReadOnlyList<StaticModelImportedMaterial> importedMaterials,
        int slotIndex)
    {
        var mesh = model.Meshes[meshIndex];
        string materialName = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < importedMaterials.Count
            ? importedMaterials[mesh.MaterialIndex].DisplayName
            : string.Empty;

        string baseSlotName = BuildReadableSlotName(nodeName, mesh.Name, materialName, slotIndex);
        string uniqueSlotName = MakeUnique(baseSlotName, usedSlotNames);
        mesh.Name = uniqueSlotName;
        mesh.SlotName = uniqueSlotName;
        return uniqueSlotName;
    }

    private static string BuildReadableSlotName(string nodeName, string meshName, string materialName, int slotIndex)
    {
        string baseName = SanitizeDisplayName(nodeName);
        if (string.IsNullOrWhiteSpace(baseName) || IsSyntheticMeshSuffix(baseName))
        {
            baseName = SanitizeDisplayName(meshName);
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = $"Mesh {slotIndex + 1}";
        }

        string cleanedMaterialName = SanitizeMaterialDisplayName(materialName);
        if (string.IsNullOrWhiteSpace(cleanedMaterialName)
            || baseName.Contains(cleanedMaterialName, StringComparison.OrdinalIgnoreCase))
        {
            return baseName;
        }

        return $"{baseName} [{cleanedMaterialName}]";
    }

    private static string BuildMaterialDisplayName(Assimp.Material material, int materialIndex, string modelFilePath)
    {
        string materialName = SanitizeMaterialDisplayName(material.Name);
        if (!string.IsNullOrWhiteSpace(materialName))
        {
            return materialName;
        }

        string? diffuseTexturePath = ResolveTextureFilePath(material, modelFilePath, TextureType.Diffuse);
        if (!string.IsNullOrWhiteSpace(diffuseTexturePath))
        {
            return SanitizeDisplayName(Path.GetFileNameWithoutExtension(diffuseTexturePath));
        }

        return $"Material {materialIndex + 1}";
    }

    private static string? ResolveNormalTextureFilePath(Assimp.Material material, string modelFilePath)
    {
        string? normalPath = ResolveTextureFilePath(material, modelFilePath, TextureType.Normals);
        if (!string.IsNullOrWhiteSpace(normalPath))
        {
            return normalPath;
        }

        string? heightPath = ResolveTextureFilePath(material, modelFilePath, TextureType.Height);
        if (!string.IsNullOrWhiteSpace(heightPath)
            && Path.GetFileNameWithoutExtension(heightPath).Contains("normal", StringComparison.OrdinalIgnoreCase))
        {
            return heightPath;
        }

        return null;
    }

    private static string? ResolveTextureFilePath(Assimp.Material material, string modelFilePath, TextureType textureType)
    {
        foreach (var slot in material.GetAllMaterialTextures())
        {
            if (slot.TextureType != textureType)
            {
                continue;
            }

            string? resolvedPath = ResolveTexturePath(modelFilePath, slot.FilePath);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return null;
    }

    private static string? ResolveTexturePath(string modelFilePath, string? texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath) || texturePath.StartsWith('*'))
        {
            return null;
        }

        string modelDirectory = Path.GetDirectoryName(modelFilePath)!;
        string candidate = Path.GetFullPath(Path.Combine(modelDirectory, texturePath));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(modelDirectory, Path.GetFileName(texturePath));
        return File.Exists(candidate)
            ? candidate
            : null;
    }

    private static string SanitizeMaterialDisplayName(string? materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return string.Empty;
        }

        string sanitized = materialName.Trim();
        sanitized = MaterialPrefixRegex.Replace(sanitized, string.Empty);
        sanitized = NumericPrefixRegex.Replace(sanitized, string.Empty);
        sanitized = MaterialSuffixRegex.Replace(sanitized, string.Empty);
        return SanitizeDisplayName(sanitized);
    }

    private static string SanitizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('_', ' ')
            .Trim();
    }

    private static bool IsSyntheticMeshSuffix(string value)
    {
        int index = value.LastIndexOf(" mesh", StringComparison.OrdinalIgnoreCase);
        if (index < 0 || index == value.Length - 1)
        {
            return false;
        }

        for (int i = index + 5; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string MakeUnique(string baseName, HashSet<string> usedNames)
    {
        string candidate = baseName;
        int suffix = 2;

        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    private static Color ToXnaColor(Color4D color)
    {
        return new Color(
            ClampByte(color.R),
            ClampByte(color.G),
            ClampByte(color.B),
            ClampByte(color.A));
    }

    private static byte ClampByte(float value)
    {
        float scaled = value <= 1.0f
            ? value * 255.0f
            : value;
        scaled = Math.Clamp(scaled, 0.0f, 255.0f);
        return (byte)scaled;
    }
}

public sealed class StaticModelImportResult
{
    public StaticModelImportResult(StaticModel model, IReadOnlyList<StaticModelImportedMaterial> materials)
    {
        Model = model;
        Materials = materials;
    }

    public StaticModel Model { get; }

    public IReadOnlyList<StaticModelImportedMaterial> Materials { get; }
}

public sealed class StaticModelImportedMaterial
{
    public int MaterialIndex { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? DiffuseTextureFilePath { get; set; }

    public string? NormalTextureFilePath { get; set; }

    public Color DiffuseColor { get; set; } = Color.White;

    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;

    public Vector3 SpecularColor { get; set; } = new(0.5f);

    public float SpecularPower { get; set; } = 16.0f;
}
