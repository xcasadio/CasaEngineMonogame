using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpGLTF.Schema2;
using System.Text.RegularExpressions;
using GltfMaterial = SharpGLTF.Schema2.Material;
using GltfNode = SharpGLTF.Schema2.Node;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Reads a glTF/GLB file into a <see cref="StaticModel"/> using SharpGLTF.
/// Produces the same <see cref="StaticModelImportResult"/> contract as the
/// legacy Assimp-based importer (geometry, node hierarchy, PBR material
/// metadata and external texture paths). No skeleton or animation is read.
/// Non-glTF formats must be converted to glTF first (editor side, AssimpNetter).
/// </summary>
public sealed class GltfStaticModelReader
{
    private const float DefaultSpecularPower = 16.0f;
    private const float MaxSupportedSpecularPower = 128.0f;
    private static readonly Regex MaterialPrefixRegex = new(@"^Material_+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericPrefixRegex = new(@"^\d+_+", RegexOptions.Compiled);
    private static readonly Regex MaterialSuffixRegex = new(@"Sub\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool IsFileSupported(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads <paramref name="filePath"/> and returns the model plus its imported
    /// material metadata. The optional <paramref name="legacyMaterialImportProfile"/>
    /// is applied to each material exactly like the legacy importer.
    /// </summary>
    public StaticModelImportResult ReadWithMetadata(string filePath, ILegacyMaterialImportProfile legacyMaterialImportProfile = null)
    {
        ModelRoot root;
        try
        {
            root = ModelRoot.Load(filePath, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.Skip });
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

        var importedMaterials = BuildMaterials(root, filePath, legacyMaterialImportProfile ?? NeutralLegacyMaterialImportProfile.Instance);

        // Flat engine mesh list (one engine mesh per glTF primitive) and a map
        // from glTF mesh logical index -> the engine mesh indices it produced.
        var engineMeshIndicesByGltfMesh = new Dictionary<int, List<int>>();
        foreach (var gltfMesh in root.LogicalMeshes)
        {
            var indices = new List<int>(gltfMesh.Primitives.Count);
            foreach (var primitive in gltfMesh.Primitives)
            {
                var modelMesh = BuildMesh(gltfMesh.Name, primitive, importedMaterials);
                indices.Add(model.Meshes.Count);
                model.Meshes.Add(modelMesh);
            }

            engineMeshIndicesByGltfMesh[gltfMesh.LogicalIndex] = indices;
        }

        var scene = root.DefaultScene ?? (root.LogicalScenes.Count > 0 ? root.LogicalScenes[0] : null);
        if (scene != null)
        {
            var syntheticRoot = new StaticModelNode { Name = "Root" };
            var usedSlotNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in scene.VisualChildren)
            {
                syntheticRoot.Children.Add(BuildNode(node, model, engineMeshIndicesByGltfMesh, importedMaterials, usedSlotNames));
            }

            // Collapse a single structural root so the hierarchy mirrors the source.
            model.RootNode = syntheticRoot.Children.Count == 1 ? syntheticRoot.Children[0] : syntheticRoot;
        }

        StaticModelMaterialSlots.EnsureMetadata(model);

        return new StaticModelImportResult(model, importedMaterials);
    }

    public StaticModel Read(string filePath, ILegacyMaterialImportProfile legacyMaterialImportProfile = null)
    {
        return ReadWithMetadata(filePath, legacyMaterialImportProfile).Model;
    }

    public IReadOnlyList<string> GetTextureFilePaths(string filePath)
    {
        var paths = new List<string>();
        StaticModelImportResult result;
        try
        {
            result = ReadWithMetadata(filePath);
        }
        catch
        {
            return Array.Empty<string>();
        }

        foreach (var material in result.Materials)
        {
            AddPath(paths, material.DiffuseTextureFilePath);
            AddPath(paths, material.NormalTextureFilePath);
            AddPath(paths, material.ReflectionTextureFilePath);
        }

        return paths;
    }

    private static void AddPath(List<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path))
        {
            paths.Add(path);
        }
    }

    // ------------------------------------------------------------------
    //  Materials
    // ------------------------------------------------------------------

    private static List<StaticModelImportedMaterial> BuildMaterials(
        ModelRoot root,
        string filePath,
        ILegacyMaterialImportProfile legacyMaterialImportProfile)
    {
        var result = new List<StaticModelImportedMaterial>(root.LogicalMaterials.Count);

        for (int i = 0; i < root.LogicalMaterials.Count; i++)
        {
            var material = root.LogicalMaterials[i];
            var importedMaterial = new StaticModelImportedMaterial
            {
                MaterialIndex = i,
                Name = material.Name ?? string.Empty,
                DisplayName = BuildMaterialDisplayName(material, i, filePath),
                DiffuseTextureFilePath = ResolveTextureFilePath(material, filePath, "BaseColor"),
                NormalTextureFilePath = ResolveTextureFilePath(material, filePath, "Normal"),
                AmbientColor = Vector3.Zero,
                DiffuseColor = ResolveBaseColor(material),
                EmissiveColor = ResolveChannelRgb(material, "Emissive"),
                SpecularColor = new Vector3(0.5f),
                SpecularPower = ResolveSpecularPower(material),
            };

            ApplyLegacyImportProfile(importedMaterial, filePath, legacyMaterialImportProfile);
            result.Add(importedMaterial);
        }

        return result;
    }

    private static Color ResolveBaseColor(GltfMaterial material)
    {
        var channel = material.FindChannel("BaseColor");
        if (channel == null)
        {
            return Color.White;
        }

        var color = channel.Value.Color;
        return new Color(ClampByte(color.X), ClampByte(color.Y), ClampByte(color.Z), ClampByte(color.W));
    }

    private static Vector3 ResolveChannelRgb(GltfMaterial material, string channelKey)
    {
        var channel = material.FindChannel(channelKey);
        if (channel == null)
        {
            return Vector3.Zero;
        }

        var color = channel.Value.Color;
        return new Vector3(color.X, color.Y, color.Z);
    }

    private static float ResolveSpecularPower(GltfMaterial material)
    {
        var channel = material.FindChannel("MetallicRoughness");
        if (channel == null)
        {
            return DefaultSpecularPower;
        }

        float roughness = Math.Clamp(channel.Value.GetFactor("RoughnessFactor"), 0.0f, 1.0f);
        return ConvertRoughnessToSpecularPower(roughness);
    }

    private static float ConvertRoughnessToSpecularPower(float roughness)
    {
        float clampedRoughness = Math.Clamp(roughness, 0.0f, 1.0f);
        if (clampedRoughness <= 0.0001f)
        {
            return MaxSupportedSpecularPower;
        }

        float exponent = (2.0f / (clampedRoughness * clampedRoughness)) - 2.0f;
        return Math.Clamp(exponent, 0.0f, MaxSupportedSpecularPower);
    }

    private static string ResolveTextureFilePath(GltfMaterial material, string modelFilePath, string channelKey)
    {
        var channel = material.FindChannel(channelKey);
        var image = channel?.Texture?.PrimaryImage;
        if (image == null)
        {
            return null;
        }

        // Only external images expose a usable file path; embedded (GLB) images
        // have no on-disk source and are skipped, matching the legacy importer.
        string sourcePath = image.Content.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        if (File.Exists(sourcePath))
        {
            return Path.GetFullPath(sourcePath);
        }

        string modelDirectory = Path.GetDirectoryName(modelFilePath)!;
        string candidate = Path.Combine(modelDirectory, Path.GetFileName(sourcePath));
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    // ------------------------------------------------------------------
    //  Geometry
    // ------------------------------------------------------------------

    private static StaticModelMesh BuildMesh(string gltfMeshName, MeshPrimitive primitive, IReadOnlyList<StaticModelImportedMaterial> importedMaterials)
    {
        var modelMesh = new StaticModelMesh
        {
            Name = gltfMeshName ?? string.Empty,
            MaterialIndex = primitive.Material?.LogicalIndex ?? -1,
        };

        var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
        int vertexCount = positions?.Count ?? 0;
        var vertices = new VertexPositionNormalTexture[vertexCount];

        for (int k = 0; k < vertexCount; k++)
        {
            var p = positions![k];
            vertices[k].Position = new Vector3(p.X, p.Y, p.Z);
        }

        var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
        bool hasNormals = normals != null && normals.Count == vertexCount;
        if (hasNormals)
        {
            for (int k = 0; k < vertexCount; k++)
            {
                var n = normals![k];
                vertices[k].Normal = new Vector3(n.X, n.Y, n.Z);
            }
        }
        else if (vertexCount > 0)
        {
            GenerateSmoothNormals(vertices, primitive, gltfMeshName);
        }

        var uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        if (uvs != null)
        {
            int count = Math.Min(uvs.Count, vertexCount);
            for (int k = 0; k < count; k++)
            {
                var uv = uvs[k];
                // glTF uses the same top-left UV origin as XNA/Direct3D: no flip.
                vertices[k].TextureCoordinate = new Vector2(uv.X, uv.Y);
            }
        }

        // Normalize to a triangle list. Reverse each triangle's winding to match
        // the legacy importer's FlipWindingOrder so existing render states hold.
        var indexList = new List<uint>(vertexCount);
        foreach (var triangle in primitive.GetTriangleIndices())
        {
            indexList.Add((uint)triangle.A);
            indexList.Add((uint)triangle.C);
            indexList.Add((uint)triangle.B);
        }

        modelMesh.SetData(vertices, indexList.ToArray());

        if (modelMesh.MaterialIndex >= 0 && modelMesh.MaterialIndex < importedMaterials.Count)
        {
            modelMesh.DiffuseTextureFilePath = importedMaterials[modelMesh.MaterialIndex].DiffuseTextureFilePath;
        }

        return modelMesh;
    }

    /// <summary>
    /// Generates smooth, area-weighted vertex normals for a primitive that ships without a
    /// NORMAL accessor (common in PSX-style glTF exports). Normals are accumulated in the same
    /// space as the positions from the glTF triangle order BEFORE this reader's winding
    /// reversal, so a spec-compliant CCW triangle yields an outward-facing normal.
    /// </summary>
    private static void GenerateSmoothNormals(VertexPositionNormalTexture[] vertices, MeshPrimitive primitive, string meshName)
    {
        var accumulated = new Vector3[vertices.Length];

        foreach (var triangle in primitive.GetTriangleIndices())
        {
            if (triangle.A >= vertices.Length || triangle.B >= vertices.Length || triangle.C >= vertices.Length)
            {
                continue;
            }

            var a = vertices[triangle.A].Position;
            var b = vertices[triangle.B].Position;
            var c = vertices[triangle.C].Position;
            var faceNormal = Vector3.Cross(b - a, c - a);

            accumulated[triangle.A] += faceNormal;
            accumulated[triangle.B] += faceNormal;
            accumulated[triangle.C] += faceNormal;
        }

        for (int k = 0; k < vertices.Length; k++)
        {
            var n = accumulated[k];
            vertices[k].Normal = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Up;
        }

        Logs.WriteInfo($"glTF mesh \"{meshName}\" has no NORMAL attribute; generated smooth vertex normals.");
    }

    private static StaticModelNode BuildNode(
        GltfNode gltfNode,
        StaticModel model,
        IReadOnlyDictionary<int, List<int>> engineMeshIndicesByGltfMesh,
        IReadOnlyList<StaticModelImportedMaterial> importedMaterials,
        HashSet<string> usedSlotNames)
    {
        var node = new StaticModelNode { Name = gltfNode.Name ?? string.Empty };

        var transform = gltfNode.LocalTransform;
        node.Position = new Vector3(transform.Translation.X, transform.Translation.Y, transform.Translation.Z);
        node.Rotation = new Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);
        node.Scale = new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);

        if (gltfNode.Mesh != null
            && engineMeshIndicesByGltfMesh.TryGetValue(gltfNode.Mesh.LogicalIndex, out var meshIndices)
            && meshIndices.Count > 0)
        {
            node.MeshIndex = meshIndices[0];
            ApplyReadableMeshName(model, node.MeshIndex, gltfNode.Name, usedSlotNames, importedMaterials, 0);

            for (int i = 1; i < meshIndices.Count; i++)
            {
                int meshIndex = meshIndices[i];
                string slotName = ApplyReadableMeshName(model, meshIndex, gltfNode.Name, usedSlotNames, importedMaterials, i);
                node.Children.Add(new StaticModelNode { Name = slotName, MeshIndex = meshIndex });
            }
        }

        foreach (var child in gltfNode.VisualChildren)
        {
            node.Children.Add(BuildNode(child, model, engineMeshIndicesByGltfMesh, importedMaterials, usedSlotNames));
        }

        return node;
    }

    // ------------------------------------------------------------------
    //  Readable naming (format-agnostic; mirrors the legacy importer)
    // ------------------------------------------------------------------

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

    private static string BuildMaterialDisplayName(GltfMaterial material, int materialIndex, string modelFilePath)
    {
        string materialName = SanitizeMaterialDisplayName(material.Name);
        if (!string.IsNullOrWhiteSpace(materialName))
        {
            return materialName;
        }

        string diffuseTexturePath = ResolveTextureFilePath(material, modelFilePath, "BaseColor");
        if (!string.IsNullOrWhiteSpace(diffuseTexturePath))
        {
            return SanitizeDisplayName(Path.GetFileNameWithoutExtension(diffuseTexturePath));
        }

        return $"Material {materialIndex + 1}";
    }

    private static string SanitizeMaterialDisplayName(string materialName)
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

    private static string SanitizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('_', ' ').Trim();
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

    private static void ApplyLegacyImportProfile(
        StaticModelImportedMaterial importedMaterial,
        string modelFilePath,
        ILegacyMaterialImportProfile legacyMaterialImportProfile)
    {
        string modelName = Path.GetFileNameWithoutExtension(modelFilePath);
        var interpretation = legacyMaterialImportProfile.Interpret(new LegacyMaterialImportContext(
            SourceAssetPath: modelFilePath,
            SourceAssetName: modelName,
            ImportedMaterial: importedMaterial));

        importedMaterial.SurfaceIntent = interpretation.SurfaceIntent;
        importedMaterial.AlphaCutoutHint = interpretation.AlphaCutout;
        importedMaterial.BrightAmbientHint = interpretation.BrightAmbient;
        importedMaterial.UsesReflection |= interpretation.Reflection;
    }

    private static byte ClampByte(float value)
    {
        float scaled = value <= 1.0f ? value * 255.0f : value;
        scaled = Math.Clamp(scaled, 0.0f, 255.0f);
        return (byte)scaled;
    }
}
