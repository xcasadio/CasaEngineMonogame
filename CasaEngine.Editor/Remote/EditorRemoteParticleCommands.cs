using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Editor.Remote;

/// <summary>Particle commands (ADR-0093, plan T2.3).</summary>
internal static class EditorRemoteParticleCommands
{
    private const string AssetArgument = "{\"type\":\"string\",\"description\":\"Particle asset id (GUID) or project-relative .particle file.\"}";

    private enum ValueKind
    {
        Float,
        Integer,
        Boolean,
        Text,
        Texture,
        ShapeEnum,
        BlendEnum,

        /// <summary>A preset name or a colour the panel parses against a closed set.</summary>
        PanelChecked,
    }

    /// <summary>
    /// The automation keys and their bounds (ADR-0093 P5, security review S2-2): the ranges of the particle inspector
    /// fields (<c>ParticleAssetInspectorPanel</c>, CreateFloatField / CreateIntField). Any key not listed is refused.
    /// </summary>
    private static readonly Dictionary<string, (ValueKind Kind, double Min, double Max)> Keys = new(StringComparer.Ordinal)
    {
        ["name"] = (ValueKind.Text, 0, 128),
        ["enabled"] = (ValueKind.Boolean, 0, 0),
        ["looping"] = (ValueKind.Boolean, 0, 0),
        ["randomframe"] = (ValueKind.Boolean, 0, 0),
        ["randomstartframe"] = (ValueKind.Boolean, 0, 0),
        ["flipbookrandomstart"] = (ValueKind.Boolean, 0, 0),
        ["duration"] = (ValueKind.Float, 0.01, 3600),
        ["maxparticles"] = (ValueKind.Integer, 1, 100000),
        ["rate"] = (ValueKind.Float, 0, 100000),
        ["rateovertime"] = (ValueKind.Float, 0, 100000),
        ["lifetimemin"] = (ValueKind.Float, 0.001, 3600),
        ["lifetimemax"] = (ValueKind.Float, 0.001, 3600),
        ["speedmin"] = (ValueKind.Float, 0, 100000),
        ["speedmax"] = (ValueKind.Float, 0, 100000),
        ["sizeminx"] = (ValueKind.Float, 0, 100000),
        ["sizeminy"] = (ValueKind.Float, 0, 100000),
        ["sizemaxx"] = (ValueKind.Float, 0, 100000),
        ["sizemaxy"] = (ValueKind.Float, 0, 100000),
        ["flipbookcolumns"] = (ValueKind.Integer, 1, 64),
        ["atlascolumns"] = (ValueKind.Integer, 1, 64),
        ["flipbookrows"] = (ValueKind.Integer, 1, 64),
        ["atlasrows"] = (ValueKind.Integer, 1, 64),
        ["flipbookframes"] = (ValueKind.Integer, 1, 4096),
        ["flipbookframecount"] = (ValueKind.Integer, 1, 4096),
        ["framecount"] = (ValueKind.Integer, 1, 4096),
        ["flipbookfps"] = (ValueKind.Float, 0, 240),
        ["framespersecond"] = (ValueKind.Float, 0, 240),
        ["texture"] = (ValueKind.Texture, 0, 0),
        ["shape"] = (ValueKind.ShapeEnum, 0, 0),
        ["blend"] = (ValueKind.BlendEnum, 0, 0),
        ["flipbookcurve"] = (ValueKind.PanelChecked, 0, 0),
        ["frameoverlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["flipbookcurvepreset"] = (ValueKind.PanelChecked, 0, 0),
        ["color"] = (ValueKind.PanelChecked, 0, 0),
        ["startcolor"] = (ValueKind.PanelChecked, 0, 0),
        ["startgradient"] = (ValueKind.PanelChecked, 0, 0),
        ["startcolorgradient"] = (ValueKind.PanelChecked, 0, 0),
        ["startcolorpreset"] = (ValueKind.PanelChecked, 0, 0),
        ["sizecurve"] = (ValueKind.PanelChecked, 0, 0),
        ["sizeoverlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["sizecurvepreset"] = (ValueKind.PanelChecked, 0, 0),
        ["alphacurve"] = (ValueKind.PanelChecked, 0, 0),
        ["alphaoverlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["alphacurvepreset"] = (ValueKind.PanelChecked, 0, 0),
        ["velocitycurve"] = (ValueKind.PanelChecked, 0, 0),
        ["velocityoverlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["velocitycurvepreset"] = (ValueKind.PanelChecked, 0, 0),
        ["colorlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["coloroverlifetime"] = (ValueKind.PanelChecked, 0, 0),
        ["colorgradient"] = (ValueKind.PanelChecked, 0, 0),
        ["colorgradientpreset"] = (ValueKind.PanelChecked, 0, 0),
    };

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "particle.create",
            "Creates <folder>/<name>.particle from a preset (default, fire, smoke, spark…) and adds it to the asset catalog. Never overwrites a file or reuses a catalogued name.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"folder\":{\"type\":\"string\",\"description\":\"Existing project folder, '' for the root.\"},\"preset\":{\"type\":\"string\"}},\"required\":[\"name\",\"folder\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteNewAssetFile file = EditorRemoteAssetCreation.Prepare(host.ProjectDirectory, call.GetString("folder", 260), call.GetString("name", 64), EditorRemoteAssetTypes.Particle);
                if (!host.CreateParticleAsset(file, call.GetOptionalString("preset", 64), out Guid assetId, out string error))
                {
                    throw new EditorRemoteArgumentException(error ?? "The particle asset was not created.");
                }

                call.Succeed(new { asset_id = assetId, file = file.RelativeFile.Replace('\\', '/') });
            }));

        registry.Register(new EditorRemoteCommand(
            "particle.get",
            "Opens a particle asset in its panel and reads its emitters (main values).",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: true,
            destructive: false,
            call =>
            {
                RemoteAssetReference asset = OpenParticle(call, host);
                ParticleEffectAsset effect = host.GetParticleAsset(asset.Info.Id)
                    ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, "The particle asset is not loaded.");
                call.Succeed(new
                {
                    asset_id = asset.Info.Id,
                    name = EditorRemoteWorldModel.Truncate(effect.Name),
                    emitters = effect.Emitters.Select((emitter, index) => new
                    {
                        index,
                        name = EditorRemoteWorldModel.Truncate(emitter.Name),
                        enabled = emitter.Enabled,
                        duration = emitter.Duration,
                        looping = emitter.Looping,
                        max_particles = emitter.MaxParticles,
                        rate = emitter.Emission.RateOverTime,
                        shape = emitter.Shape.ShapeType.ToString(),
                        blend = emitter.Renderer.BlendMode.ToString(),
                        texture = emitter.Renderer.TextureAssetId,
                    }).ToList(),
                    keys = Keys.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                });
            }));

        registry.Register(new EditorRemoteCommand(
            "particle.set_property",
            "Sets a particle property through the particle panel's undo history (history.undo with context = the particle id). Keys: 'key' (emitter 0), 'emitterN.key' or 'asset.name'; see particle.get for the keys. Values are bounded like the inspector fields.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"property\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"asset\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenParticle(call, host);
                string property = call.GetString("property", 128);
                string raw = EditorRemoteMaterialCommands.ToRawValue(call.GetArgument("value"));
                if (!host.TrySetParticleProperty(asset.Info.Id, property, raw, Validate, out string error))
                {
                    throw new EditorRemoteArgumentException(error);
                }

                call.Succeed(new { asset_id = asset.Info.Id, property });
            }));

        registry.Register(new EditorRemoteCommand(
            "particle.save",
            "Saves one particle asset from its open panel.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenParticle(call, host);
                if (!host.TrySaveAsset(asset.Info.Id, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The particle asset was not saved.");
                }

                call.Succeed(new { saved = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "particle.drop",
            "Puts a particle asset on an entity: its particle system component takes the asset, or one is added. Undoable in the world history.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":{\"type\":\"string\"},\"asset\":" + AssetArgument + "},\"required\":[\"entity_id\",\"asset\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.Particle);
                var component = EditorWorldOperations.DropParticleAsset(
                    EditorRemoteEditCommands.WorldContext,
                    entity,
                    asset.Info.Id,
                    c => host.Select(entity, c),
                    () => host.Select(entity, null));
                host.RefreshWorldViews();
                call.Succeed(new { component_id = component.Id });
            }));
    }

    /// <summary>The P5 bounds of a normalised automation key and its text value; null when allowed.</summary>
    public static string Validate(string normalizedKey, string rawValue)
    {
        if (!Keys.TryGetValue(normalizedKey, out var rule))
        {
            return $"'{normalizedKey}' is not a particle property a command may set (see particle.get).";
        }

        string text = rawValue.Trim();
        switch (rule.Kind)
        {
            case ValueKind.Float:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)
                    ? CheckRange(normalizedKey, number, rule.Min, rule.Max)
                    : $"'{normalizedKey}' expects a finite number.";
            case ValueKind.Integer:
                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)
                    ? CheckRange(normalizedKey, integer, rule.Min, rule.Max)
                    : $"'{normalizedKey}' expects an integer.";
            case ValueKind.Boolean:
                return bool.TryParse(text, out _) ? null : $"'{normalizedKey}' expects true or false.";
            case ValueKind.Text:
                return rawValue.Length <= rule.Max ? null : $"'{normalizedKey}' is limited to {rule.Max} characters.";
            case ValueKind.Texture:
                if (!Guid.TryParse(text, out Guid textureId))
                {
                    return $"'{normalizedKey}' expects a texture asset id.";
                }

                AssetInfo texture = textureId == Guid.Empty ? null : AssetCatalog.Get(textureId);
                return textureId == Guid.Empty || (texture != null && AssetInfo.InferAssetType(texture.FileName) == EditorRemoteMaterialCommands.TextureAssetType)
                    ? null
                    : $"{textureId} is not a texture asset of the catalog.";
            case ValueKind.ShapeEnum:
                return CheckEnumName<ParticleShapeType>(normalizedKey, text);
            case ValueKind.BlendEnum:
                return CheckEnumName<ParticleBlendMode>(normalizedKey, text);
            default:
                return rawValue.Length <= EditorRemoteValidation.MaxPropertyTextLength ? null : $"'{normalizedKey}' value is too long.";
        }
    }

    private static string CheckRange(string key, double value, double min, double max)
        => value >= min && value <= max
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"'{key}' must be within [{min}, {max}].");

    /// <summary>A defined name only: never a number (Enum.TryParse would accept "7").</summary>
    private static string CheckEnumName<TEnum>(string key, string text) where TEnum : struct, Enum
    {
        foreach (string name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return $"'{key}' expects one of {string.Join(", ", Enum.GetNames<TEnum>())}.";
    }

    private static RemoteAssetReference OpenParticle(EditorRemoteCall call, IEditorRemoteHost host)
    {
        RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.Particle);
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
