using Assimp;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Globalization;
using System.Text.RegularExpressions;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Imports a 3-D file (FBX, OBJ, GLTF, …) as a <see cref="StaticModel"/> asset
/// using AssimpNet. Geometry, hierarchy, texture paths and legacy .X effect
/// metadata used by RacingGame scenery materials are preserved. No skeleton or
/// animation data is read.
/// </summary>
public class StaticModelImporter
{
    private readonly AssimpContext _assimpContext = new();
    private const string GltfRoughnessFactorPropertyName = "$mat.gltf.pbrMetallicRoughness.roughnessFactor";
    private const float DefaultSpecularPower = 16.0f;
    private const float GltfGlossinessScale = 1000.0f;
    private const float MaxSupportedSpecularPower = 128.0f;
    private static readonly Regex MaterialPrefixRegex = new(@"^Material_+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericPrefixRegex = new(@"^\d+_+", RegexOptions.Compiled);
    private static readonly Regex MaterialSuffixRegex = new(@"Sub\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private sealed class LegacyEffectInstance
    {
        public string MaterialName { get; init; } = string.Empty;

        public string EffectFilePath { get; set; } = string.Empty;

        public Dictionary<string, int> Dwords { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, float[]> Floats { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Strings { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsFileSupported(string fileName) =>
        _assimpContext.GetSupportedImportFormats().Contains(
            Path.GetExtension(fileName).ToLower());

    public StaticModelImportResult ImportWithMetadata(string filePath, ILegacyMaterialImportProfile legacyMaterialImportProfile = null)
    {
        Assimp.Scene scene;
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

        var importedMaterials = BuildMaterials(
            scene,
            filePath,
            ParseLegacyEffectInstances(filePath),
            legacyMaterialImportProfile ?? NeutralLegacyMaterialImportProfile.Instance);

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
    public StaticModel Import(string filePath, ILegacyMaterialImportProfile legacyMaterialImportProfile = null)
    {
        return ImportWithMetadata(filePath, legacyMaterialImportProfile).Model;
    }

    /// <summary>
    /// Returns all diffuse texture file paths embedded in the file,
    /// useful to trigger texture import alongside the model.
    /// </summary>
    public IReadOnlyList<string> GetTextureFilePaths(string filePath)
    {
        var paths = new List<string>();
        Assimp.Scene scene;
        try
        {
            scene = ImportScene(filePath, PostProcessSteps.None);
        }
        catch
        {
            return Array.Empty<string>();
        }

        foreach (var material in BuildMaterials(
                 scene,
                 filePath,
                 ParseLegacyEffectInstances(filePath),
                 NeutralLegacyMaterialImportProfile.Instance))
        {
            if (!string.IsNullOrWhiteSpace(material.DiffuseTextureFilePath) && !paths.Contains(material.DiffuseTextureFilePath))
            {
                paths.Add(material.DiffuseTextureFilePath);
            }

            if (!string.IsNullOrWhiteSpace(material.NormalTextureFilePath) && !paths.Contains(material.NormalTextureFilePath))
            {
                paths.Add(material.NormalTextureFilePath);
            }

            if (!string.IsNullOrWhiteSpace(material.ReflectionTextureFilePath) && !paths.Contains(material.ReflectionTextureFilePath))
            {
                paths.Add(material.ReflectionTextureFilePath);
            }
        }

        return paths;
    }

    private Assimp.Scene ImportScene(string filePath, PostProcessSteps postProcessSteps)
    {
        return _assimpContext.ImportFile(filePath, postProcessSteps);
    }

    private static List<StaticModelImportedMaterial> BuildMaterials(
        Assimp.Scene scene,
        string filePath,
        IReadOnlyDictionary<string, LegacyEffectInstance> legacyEffectsByMaterial,
        ILegacyMaterialImportProfile legacyMaterialImportProfile)
    {
        var result = new List<StaticModelImportedMaterial>();
        if (scene == null)
        {
            return result;
        }

        for (int i = 0; i < scene.Materials.Count; i++)
        {
            var material = scene.Materials[i];
            var importedMaterial = new StaticModelImportedMaterial
            {
                MaterialIndex = i,
                Name = material.Name ?? string.Empty,
                DisplayName = BuildMaterialDisplayName(material, i, filePath),
                DiffuseTextureFilePath = ResolveTextureFilePath(material, filePath, TextureType.Diffuse),
                NormalTextureFilePath = ResolveNormalTextureFilePath(material, filePath),
                AmbientColor = Vector3.Zero,
                DiffuseColor = material.HasColorDiffuse
                    ? ToXnaColor(material.ColorDiffuse)
                    : Color.White,
                EmissiveColor = material.HasColorEmissive
                    ? new Vector3(material.ColorEmissive.R, material.ColorEmissive.G, material.ColorEmissive.B)
                    : Vector3.Zero,
                SpecularColor = material.HasColorSpecular
                    ? new Vector3(material.ColorSpecular.R, material.ColorSpecular.G, material.ColorSpecular.B)
                    : new Vector3(0.5f),
                SpecularPower = ResolveSpecularPower(material, filePath),
            };

            if (legacyEffectsByMaterial.TryGetValue(importedMaterial.Name, out LegacyEffectInstance effectInstance))
            {
                ApplyLegacyEffectMetadata(importedMaterial, effectInstance, filePath);
            }

            ApplyLegacyImportProfile(importedMaterial, filePath, legacyMaterialImportProfile);

            result.Add(importedMaterial);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, LegacyEffectInstance> ParseLegacyEffectInstances(string filePath)
    {
        var result = new Dictionary<string, LegacyEffectInstance>(StringComparer.Ordinal);
        if (!Path.GetExtension(filePath).Equals(".x", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(filePath))
        {
            return result;
        }

        string text = File.ReadAllText(filePath);
        int searchIndex = 0;

        while (true)
        {
            int materialIndex = IndexOfToken(text, "Material", searchIndex);
            if (materialIndex < 0)
            {
                break;
            }

            int nameIndex = materialIndex + "Material".Length;
            SkipWhitespace(text, ref nameIndex);

            string materialName = ReadIdentifier(text, ref nameIndex);
            if (string.IsNullOrWhiteSpace(materialName))
            {
                searchIndex = materialIndex + "Material".Length;
                continue;
            }

            int braceOpenIndex = text.IndexOf('{', nameIndex);
            if (braceOpenIndex < 0)
            {
                break;
            }

            string materialBody = ExtractBraceBlock(text, braceOpenIndex, out int braceCloseIndex);
            LegacyEffectInstance effectInstance = ParseLegacyEffectInstance(materialName, materialBody);
            if (effectInstance != null)
            {
                result[materialName] = effectInstance;
            }

            searchIndex = braceCloseIndex + 1;
        }

        return result;
    }

    private static LegacyEffectInstance ParseLegacyEffectInstance(string materialName, string materialBody)
    {
        int searchIndex = 0;
        LegacyEffectInstance lastInstance = null;

        while (true)
        {
            int effectIndex = IndexOfToken(materialBody, "EffectInstance", searchIndex);
            if (effectIndex < 0)
            {
                break;
            }

            int braceOpenIndex = materialBody.IndexOf('{', effectIndex);
            if (braceOpenIndex < 0)
            {
                break;
            }

            string effectBody = ExtractBraceBlock(materialBody, braceOpenIndex, out int braceCloseIndex);
            LegacyEffectInstance effectInstance = ParseSingleLegacyEffectInstance(materialName, effectBody);
            if (!string.IsNullOrWhiteSpace(effectInstance.EffectFilePath))
            {
                lastInstance = effectInstance;
            }

            searchIndex = braceCloseIndex + 1;
        }

        return lastInstance;
    }

    private static LegacyEffectInstance ParseSingleLegacyEffectInstance(string materialName, string effectBody)
    {
        var effectInstance = new LegacyEffectInstance
        {
            MaterialName = materialName,
        };

        Match fileMatch = Regex.Match(
            effectBody,
            "EffectFilename\\s*\\{\\s*\"(?<path>[^\"]+\\.fx)\"\\s*;\\s*\\}",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!fileMatch.Success)
        {
            fileMatch = Regex.Match(
                effectBody,
                "\"(?<path>[^\"]+\\.fx)\"\\s*;",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        if (fileMatch.Success)
        {
            effectInstance.EffectFilePath = fileMatch.Groups["path"].Value;
        }

        foreach (Match match in Regex.Matches(
                     effectBody,
                     "EffectParamDWord\\s*\\{\\s*\"(?<name>[^\"]+)\"\\s*;\\s*(?<value>\\d+)\\s*;\\s*\\}",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            if (int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                effectInstance.Dwords[match.Groups["name"].Value] = value;
            }
        }

        foreach (Match match in Regex.Matches(
                     effectBody,
                     "EffectParamString\\s*\\{\\s*\"(?<name>[^\"]+)\"\\s*;\\s*\"(?<value>[^\"]*)\"\\s*;\\s*\\}",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            effectInstance.Strings[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        foreach (Match match in Regex.Matches(
                     effectBody,
                     "EffectParamFloats\\s*\\{\\s*\"(?<name>[^\"]+)\"\\s*;\\s*(?<count>\\d+)\\s*;\\s*(?<values>[^;]+?)\\s*;\\s*\\}",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            float[] values = ParseFloatList(match.Groups["values"].Value);
            if (values.Length > 0)
            {
                effectInstance.Floats[match.Groups["name"].Value] = values;
            }
        }

        return effectInstance;
    }

    private static void ApplyLegacyEffectMetadata(
        StaticModelImportedMaterial importedMaterial,
        LegacyEffectInstance effectInstance,
        string modelFilePath)
    {
        importedMaterial.EffectFilePath = ResolveRelativePath(modelFilePath, effectInstance.EffectFilePath);

        if (effectInstance.Dwords.TryGetValue("technique", out int techniqueIndex))
        {
            importedMaterial.LegacyTechniqueIndex = techniqueIndex;
        }

        if (TryReadVector3(effectInstance.Floats, "ambientColor", out Vector3 ambientColor))
        {
            importedMaterial.AmbientColor = ambientColor;
        }

        if (TryReadColor(effectInstance.Floats, "diffuseColor", out Color diffuseColor))
        {
            importedMaterial.DiffuseColor = diffuseColor;
        }

        if (TryReadVector3(effectInstance.Floats, "specularColor", out Vector3 specularColor))
        {
            importedMaterial.SpecularColor = specularColor;
        }

        if (TryReadFloat(effectInstance.Floats, "shininess", out float specularPower))
        {
            importedMaterial.SpecularPower = specularPower;
        }

        if (effectInstance.Strings.TryGetValue("diffuseTexture", out string diffuseTexturePath))
        {
            importedMaterial.DiffuseTextureFilePath = ResolveTexturePath(modelFilePath, diffuseTexturePath) ?? importedMaterial.DiffuseTextureFilePath;
        }

        if (effectInstance.Strings.TryGetValue("normalTexture", out string normalTexturePath))
        {
            importedMaterial.NormalTextureFilePath = ResolveTexturePath(modelFilePath, normalTexturePath) ?? importedMaterial.NormalTextureFilePath;
        }

        if (effectInstance.Strings.TryGetValue("reflectionCubeTexture", out string reflectionTexturePath))
        {
            importedMaterial.ReflectionTextureFilePath = ResolveTexturePath(modelFilePath, reflectionTexturePath)
                ?? ResolveRelativePath(modelFilePath, reflectionTexturePath);
        }

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

    private static bool TryReadColor(
        IReadOnlyDictionary<string, float[]> valuesByName,
        string key,
        out Color color)
    {
        color = Color.White;
        if (!valuesByName.TryGetValue(key, out float[] values) || values.Length < 3)
        {
            return false;
        }

        float alpha = values.Length >= 4 ? values[3] : 1.0f;
        color = new Color(
            ClampByte(values[0]),
            ClampByte(values[1]),
            ClampByte(values[2]),
            ClampByte(alpha));
        return true;
    }

    private static bool TryReadVector3(
        IReadOnlyDictionary<string, float[]> valuesByName,
        string key,
        out Vector3 vector)
    {
        vector = Vector3.Zero;
        if (!valuesByName.TryGetValue(key, out float[] values) || values.Length < 3)
        {
            return false;
        }

        vector = new Vector3(values[0], values[1], values[2]);
        return true;
    }

    private static bool TryReadFloat(
        IReadOnlyDictionary<string, float[]> valuesByName,
        string key,
        out float value)
    {
        value = 0.0f;
        if (!valuesByName.TryGetValue(key, out float[] values) || values.Length == 0)
        {
            return false;
        }

        value = values[0];
        return true;
    }

    private static float[] ParseFloatList(string valueList)
    {
        string normalized = valueList.Replace(";", string.Empty);
        string[] parts = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<float>(parts.Length);

        for (int i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static float ResolveSpecularPower(Material material, string modelFilePath)
    {
        if (TryResolveGltfRoughness(material, modelFilePath, out float roughness))
        {
            return ConvertRoughnessToSpecularPower(roughness);
        }

        return material.HasShininess
            ? material.Shininess
            : DefaultSpecularPower;
    }

    private static bool TryResolveGltfRoughness(Material material, string modelFilePath, out float roughness)
    {
        roughness = 0.0f;

        string extension = Path.GetExtension(modelFilePath);
        if (!extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryReadFloatProperty(material, GltfRoughnessFactorPropertyName, out roughness))
        {
            roughness = Math.Clamp(roughness, 0.0f, 1.0f);
            return true;
        }

        if (!material.HasShininess)
        {
            return false;
        }

        roughness = 1.0f - Math.Clamp(material.Shininess / GltfGlossinessScale, 0.0f, 1.0f);
        return true;
    }

    private static bool TryReadFloatProperty(Material material, string propertyName, out float value)
    {
        value = 0.0f;
        if (!material.HasProperty(propertyName))
        {
            return false;
        }

        MaterialProperty property = material.GetProperty(propertyName);
        if (property == null
            || property.PropertyType != PropertyType.Float
            || !property.HasRawData
            || property.RawData == null
            || property.RawData.Length < sizeof(float))
        {
            return false;
        }

        value = BitConverter.ToSingle(property.RawData, 0);
        return true;
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

    private static string BuildMaterialDisplayName(Material material, int materialIndex, string modelFilePath)
    {
        string materialName = SanitizeMaterialDisplayName(material.Name);
        if (!string.IsNullOrWhiteSpace(materialName))
        {
            return materialName;
        }

        string diffuseTexturePath = ResolveTextureFilePath(material, modelFilePath, TextureType.Diffuse);
        if (!string.IsNullOrWhiteSpace(diffuseTexturePath))
        {
            return SanitizeDisplayName(Path.GetFileNameWithoutExtension(diffuseTexturePath));
        }

        return $"Material {materialIndex + 1}";
    }

    private static string ResolveNormalTextureFilePath(Material material, string modelFilePath)
    {
        string normalPath = ResolveTextureFilePath(material, modelFilePath, TextureType.Normals);
        if (!string.IsNullOrWhiteSpace(normalPath))
        {
            return normalPath;
        }

        string heightPath = ResolveTextureFilePath(material, modelFilePath, TextureType.Height);
        if (!string.IsNullOrWhiteSpace(heightPath)
            && Path.GetFileNameWithoutExtension(heightPath).Contains("normal", StringComparison.OrdinalIgnoreCase))
        {
            return heightPath;
        }

        return null;
    }

    private static string ResolveTextureFilePath(Material material, string modelFilePath, TextureType textureType)
    {
        foreach (var slot in material.GetAllMaterialTextures())
        {
            if (slot.TextureType != textureType)
            {
                continue;
            }

            string resolvedPath = ResolveTexturePath(modelFilePath, slot.FilePath);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return null;
    }

    private static string ResolveTexturePath(string modelFilePath, string texturePath)
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

    private static string ResolveRelativePath(string modelFilePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        string modelDirectory = Path.GetDirectoryName(modelFilePath)!;
        return Path.GetFullPath(Path.Combine(modelDirectory, relativePath));
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

    private static int IndexOfToken(string text, string token, int startIndex)
        => text.IndexOf(token, startIndex, StringComparison.OrdinalIgnoreCase);

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static string ReadIdentifier(string text, ref int index)
    {
        SkipWhitespace(text, ref index);

        int start = index;
        while (index < text.Length)
        {
            char character = text[index];
            if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
            {
                index++;
                continue;
            }

            break;
        }

        return text.Substring(start, index - start).Trim();
    }

    private static string ExtractBraceBlock(string text, int braceOpenIndex, out int braceCloseIndex)
    {
        int depth = 0;

        for (int i = braceOpenIndex; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '{')
            {
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                {
                    braceCloseIndex = i;
                    return text.Substring(braceOpenIndex + 1, i - braceOpenIndex - 1);
                }
            }
        }

        throw new InvalidOperationException("Unmatched braces while parsing .x file.");
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

    public string DiffuseTextureFilePath { get; set; }

    public string NormalTextureFilePath { get; set; }

    public string ReflectionTextureFilePath { get; set; }

    public string EffectFilePath { get; set; }

    public int LegacyTechniqueIndex { get; set; } = -1;

    public LegacyMaterialSurfaceIntent SurfaceIntent { get; set; } = LegacyMaterialSurfaceIntent.OpaqueLit;

    public bool UsesReflection { get; set; }

    public bool AlphaCutoutHint { get; set; }

    public bool BrightAmbientHint { get; set; }

    public Vector3 AmbientColor { get; set; } = Vector3.Zero;

    public Color DiffuseColor { get; set; } = Color.White;

    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;

    public Vector3 SpecularColor { get; set; } = new(0.5f);

    public float SpecularPower { get; set; } = 16.0f;
}
