using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The glTF probe and the normalised snapshot used by the material template tests of the editor import (T9.2).
/// The probe has six materials (opaque with the five textures, masked double sided, blended, opaque double sided, a plain one
/// without any block, an unlit one) and one primitive per material.
/// </summary>
internal static class GltfImportProbe
{
    public const string ProbeFileName = "pbrprobe.gltf";

    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    private static readonly string[] ImageNames = ["base.png", "normal.png", "mr.png", "occlusion.png", "emissive.png"];

    /// <summary>Writes the probe and its five 1x1 images in <paramref name="directory"/> and returns the path of the .gltf.</summary>
    public static string Write(string directory)
    {
        byte[] png = Convert.FromBase64String(PngBase64);
        foreach (string imageName in ImageNames)
        {
            File.WriteAllBytes(Path.Combine(directory, imageName), png);
        }

        string encodedBuffer = Convert.ToBase64String(CreateTriangleBuffer());
        string gltfPath = Path.Combine(directory, ProbeFileName);

        string json = $$"""
        {
          "asset": { "version": "2.0" },
          "extensionsUsed": [ "KHR_materials_unlit" ],
          "scene": 0,
          "scenes": [ { "nodes": [ 0 ] } ],
          "nodes": [ { "mesh": 0 } ],
          "meshes": [
            { "primitives": [
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 0 },
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 1 },
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 2 },
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 3 },
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 4 },
              { "attributes": { "POSITION": 0 }, "indices": 1, "material": 5 }
            ] }
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

    /// <summary>
    /// Reads every file the import wrote in <paramref name="directory"/> (the file names, then the .material, .staticmodel and
    /// .texture documents in file name order) into one document. Every GUID string is replaced by a placeholder numbered in order of
    /// first appearance, so that two imports of the same input compare equal whatever ids they generated, and a reference to
    /// the same asset stays a reference to the same placeholder.
    /// </summary>
    public static JObject Snapshot(string directory)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fileNames = new JArray();
        var documents = new JObject();

        string[] files = Directory.GetFiles(directory);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            fileNames.Add(name);
            string extension = Path.GetExtension(file);
            if (extension.ToLowerInvariant() is ".material" or ".staticmodel" or ".texture")
            {
                documents[name] = Normalize(JToken.Parse(File.ReadAllText(file)), placeholders);
            }
        }

        return new JObject { ["files"] = fileNames, ["documents"] = documents };
    }

    public static string ToText(JObject snapshot) => snapshot.ToString(Formatting.Indented).ReplaceLineEndings("\n");

    private static JToken Normalize(JToken token, Dictionary<string, string> placeholders)
    {
        switch (token)
        {
            case JObject obj:
                var normalizedObject = new JObject();
                foreach (var property in obj.Properties())
                {
                    normalizedObject[property.Name] = Normalize(property.Value, placeholders);
                }

                return normalizedObject;
            case JArray array:
                var normalizedArray = new JArray();
                foreach (var item in array)
                {
                    normalizedArray.Add(Normalize(item, placeholders));
                }

                return normalizedArray;
            case JValue { Type: JTokenType.String } value when Guid.TryParse((string?)value.Value, out Guid guid) && guid != Guid.Empty:
                string key = guid.ToString();
                if (!placeholders.TryGetValue(key, out string? placeholder))
                {
                    placeholder = $"<guid-{placeholders.Count + 1}>";
                    placeholders[key] = placeholder;
                }

                return new JValue(placeholder);
            default:
                return token.DeepClone();
        }
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
