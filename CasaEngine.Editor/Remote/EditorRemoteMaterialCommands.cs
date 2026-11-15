using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials.Definitions;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Materials.Serialization;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Remote;

/// <summary>Material commands (ADR-0093, plan T2.2).</summary>
internal static class EditorRemoteMaterialCommands
{
    public const string DefaultDefinitionId = "lit-diffuse";
    public const string TextureAssetType = "texture";

    private const string AssetArgument = "{\"type\":\"string\",\"description\":\"Material asset id (GUID) or project-relative .material file.\"}";

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "material.create",
            "Creates <folder>/<name>.material from a material definition (default 'lit-diffuse') and adds it to the asset catalog. Never overwrites a file or reuses a catalogued name.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"folder\":{\"type\":\"string\",\"description\":\"Existing project folder, '' for the root.\"},\"definition\":{\"type\":\"string\"}},\"required\":[\"name\",\"folder\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteNewAssetFile file = EditorRemoteAssetCreation.Prepare(host.ProjectDirectory, call.GetString("folder", 260), call.GetString("name", 64), EditorRemoteAssetTypes.Material);
                string definitionId = call.GetOptionalString("definition", 64) ?? DefaultDefinitionId;
                if (!MaterialDefinitionRegistry.TryGetById(definitionId, out _))
                {
                    throw new EditorRemoteArgumentException($"'{definitionId}' is not a material definition.");
                }

                var material = new MaterialAsset(definitionId)
                {
                    Name = file.Name,
                    FileName = file.RelativeFile,
                };
                material.AssetId = material.Id;
                var document = new JObject();
                MaterialAssetJsonSerializer.Save(material, document);
                EditorRemoteAssetCreation.WriteNew(file.FullPath, document.ToString(Newtonsoft.Json.Formatting.Indented));
                EditorAssetCatalogService.Add(material);
                host.SaveAssetCatalog();
                call.Succeed(new { asset_id = material.Id, file = file.RelativeFile.Replace('\\', '/') });
            }));

        registry.Register(new EditorRemoteCommand(
            "material.get",
            "Opens a material in its panel and reads its properties: key, type, effective value, whether it is set locally, bounds and enum options.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: true,
            destructive: false,
            call =>
            {
                RemoteAssetReference asset = OpenMaterial(call, host);
                call.Succeed(new { asset_id = asset.Info.Id, properties = host.GetMaterialProperties(asset.Info.Id) });
            }));

        registry.Register(new EditorRemoteCommand(
            "material.set_property",
            "Sets a material property through the material panel's undo history (history.undo with context = the material id). Values: numbers, true/false, [r,g,b(,a)] colours, [x,y,z] vectors, texture asset ids, enum option values, text. Bounds come from the material definition.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"property\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"asset\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenMaterial(call, host);
                string property = call.GetString("property", 128);
                string raw = ToRawValue(call.GetArgument("value"));
                if (!host.TrySetMaterialProperty(asset.Info.Id, property, raw, Validate, out string error))
                {
                    throw new EditorRemoteArgumentException(error);
                }

                call.Succeed(new { asset_id = asset.Info.Id, property });
            }));

        registry.Register(new EditorRemoteCommand(
            "material.save",
            "Saves one material from its open panel.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenMaterial(call, host);
                if (!host.TrySaveAsset(asset.Info.Id, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The material was not saved.");
                }

                call.Succeed(new { saved = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "component.material_override",
            "Sets the material of a static model slot (slot = index from 0), or clears it with material = null. Undoable in the world history.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":{\"type\":\"string\"},\"component_id\":{\"type\":\"string\"},\"slot\":{\"type\":\"integer\",\"minimum\":0},\"material\":{\"type\":[\"string\",\"null\"]}},\"required\":[\"entity_id\",\"component_id\",\"slot\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                Guid componentId = call.GetGuid("component_id");
                if (EditorRemoteWorldModel.FindComponent(entity, componentId) is not StaticModelComponent component)
                {
                    throw new EditorRemoteArgumentException($"Component {componentId} is not a static model of entity {entity.Id}.");
                }

                IReadOnlyList<StaticModelMaterialSlot> slots = component.GetMaterialSlots();
                int slotIndex = call.GetOptionalInt("slot", -1, 0, int.MaxValue);
                if (slotIndex < 0 || slotIndex >= slots.Count)
                {
                    throw new EditorRemoteArgumentException($"Slot {slotIndex} does not exist: the model has {slots.Count} material slot(s).");
                }

                Guid materialId = Guid.Empty;
                if (call.HasArgument("material"))
                {
                    materialId = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("material", 260), EditorRemoteAssetTypes.Material).Info.Id;
                }

                bool changed = EditorWorldOperations.SetMaterialOverride(EditorRemoteEditCommands.WorldContext, component, slots[slotIndex], materialId);
                host.RefreshWorldViews();
                call.Succeed(new { changed });
            }));
    }

    /// <summary>
    /// The value bounds of ADR-0093 P5 for a material property (security review S2-2): finite numbers inside the
    /// definition's <see cref="MaterialPropertyDefinition.MinValue"/> / <see cref="MaterialPropertyDefinition.MaxValue"/>,
    /// enum values among the options, catalogued texture assets, text of 4096 characters at most. Null when allowed.
    /// </summary>
    public static string Validate(MaterialPropertyDefinition definition, MaterialValue value)
    {
        switch (definition.ValueType)
        {
            case MaterialPropertyType.Float:
                return value.TryGetFloat(out float number) ? CheckNumber(definition, number) : "Expected a number.";
            case MaterialPropertyType.Integer:
                return value.TryGetInteger(out int integer) ? CheckNumber(definition, integer) : "Expected an integer.";
            case MaterialPropertyType.Vector2:
                return value.TryGetVector2(out Vector2 v2) && float.IsFinite(v2.X) && float.IsFinite(v2.Y) ? null : "Expected finite numbers.";
            case MaterialPropertyType.Vector3:
                return value.TryGetVector3(out Vector3 v3) && float.IsFinite(v3.X) && float.IsFinite(v3.Y) && float.IsFinite(v3.Z) ? null : "Expected finite numbers.";
            case MaterialPropertyType.Vector4:
                return value.TryGetVector4(out Vector4 v4) && float.IsFinite(v4.X) && float.IsFinite(v4.Y) && float.IsFinite(v4.Z) && float.IsFinite(v4.W) ? null : "Expected finite numbers.";
            case MaterialPropertyType.Texture:
                if (!value.TryGetTextureId(out Guid textureId))
                {
                    return "Expected a texture asset id.";
                }

                if (textureId == Guid.Empty)
                {
                    return null;
                }

                AssetInfo texture = AssetCatalog.Get(textureId);
                return texture != null && string.Equals(AssetInfo.InferAssetType(texture.FileName), TextureAssetType, StringComparison.Ordinal)
                    ? null
                    : $"{textureId} is not a texture asset of the catalog.";
            case MaterialPropertyType.Enum:
                if (!value.TryGetEnum(out string option))
                {
                    return "Expected an option value.";
                }

                for (int i = 0; i < definition.Options.Count; i++)
                {
                    if (string.Equals(definition.Options[i].Value, option, StringComparison.Ordinal))
                    {
                        return null;
                    }
                }

                return $"'{option}' is not an option of '{definition.Key}'.";
            case MaterialPropertyType.String:
                return value.TryGetString(out string text) && (text?.Length ?? 0) <= EditorRemoteValidation.MaxPropertyTextLength
                    ? null
                    : $"Expected a text of {EditorRemoteValidation.MaxPropertyTextLength} characters at most.";
            default:
                return null;
        }
    }

    /// <summary>The text form the material parser reads, from a JSON value: numbers, true/false, arrays as "a,b,c", strings as is.</summary>
    public static string ToRawValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.String:
                string text = value.GetString();
                return text.Length <= EditorRemoteValidation.MaxPropertyTextLength
                    ? text
                    : throw new EditorRemoteArgumentException($"A value is limited to {EditorRemoteValidation.MaxPropertyTextLength} characters.");
            case JsonValueKind.Array when value.GetArrayLength() <= 4:
                var builder = new StringBuilder();
                foreach (JsonElement component in value.EnumerateArray())
                {
                    if (component.ValueKind != JsonValueKind.Number)
                    {
                        throw new EditorRemoteArgumentException("Array values hold numbers only.");
                    }

                    builder.Append(builder.Length > 0 ? "," : string.Empty).Append(component.GetRawText());
                }

                return builder.ToString();
            default:
                throw new EditorRemoteArgumentException("A value is a number, true/false, a string or an array of up to 4 numbers.");
        }
    }

    private static string CheckNumber(MaterialPropertyDefinition definition, float number)
    {
        if (!float.IsFinite(number))
        {
            return "Expected a finite number.";
        }

        if ((definition.MinValue.HasValue && number < definition.MinValue.Value) || (definition.MaxValue.HasValue && number > definition.MaxValue.Value))
        {
            return string.Create(CultureInfo.InvariantCulture, $"'{definition.Key}' must be within [{definition.MinValue?.ToString(CultureInfo.InvariantCulture) ?? "-∞"}, {definition.MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "∞"}].");
        }

        return null;
    }

    private static RemoteAssetReference OpenMaterial(EditorRemoteCall call, IEditorRemoteHost host)
    {
        RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.Material);
        if (host.OpenAsset(asset.FullPath, asset.Type) == null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' could not be opened: see logs.read.");
        }

        return asset;
    }

    private static void RequireEditable(IEditorRemoteHost host)
    {
        if (host.IsPlaySessionActive)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
        }
    }
}
