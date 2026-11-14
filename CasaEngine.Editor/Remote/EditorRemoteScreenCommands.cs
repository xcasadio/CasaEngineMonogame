using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Inspector;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Remote;

/// <summary>A property of a screen node as <c>screen.get</c> reports it.</summary>
internal sealed record RemoteScreenProperty(string Name, string Type, string Value, bool Editable, IReadOnlyList<string> AssetTypes);

/// <summary>A screen node as <c>screen.get</c> reports it: flat, with its parent's id.</summary>
internal sealed record RemoteScreenNode(
    Guid Id,
    Guid? ParentId,
    string Name,
    string ControlType,
    IReadOnlyList<RemoteScreenProperty> Properties,
    IReadOnlyList<string> OtherProperties);

/// <summary>
/// UI screen commands (ADR-0093, plan T2.4): read the node tree, set a property through the screen panel's history.
/// A property is set only when the property registry of the inspector lists it as editable for the node's control type
/// (plan P12), with the inspector's value rules plus the bounds of P5.
/// </summary>
internal static class EditorRemoteScreenCommands
{
    /// <summary>Most nodes one <c>screen.get</c> returns.</summary>
    public const int MaxNodes = 1000;

    private const string AssetArgument = "{\"type\":\"string\",\"description\":\"UI screen asset id (GUID) or project-relative .uiscreen file.\"}";

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "screen.get",
            $"Opens a UI screen in its panel and reads its nodes (at most {MaxNodes}, depth first): id, parent id, name, control type, the inspector's properties with their values (bindings as markup) and the names of other set properties.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: true,
            destructive: false,
            call =>
            {
                RemoteAssetReference asset = OpenScreen(call, host);
                UIScreenDocument document = RequireDocument(host, asset);
                var nodes = new List<RemoteScreenNode>();
                bool truncated = false;
                if (document.Root != null)
                {
                    truncated = !Describe(document.Root, null, nodes);
                }

                call.Succeed(new { asset_id = asset.Info.Id, nodes, truncated });
            }));

        registry.Register(new EditorRemoteCommand(
            "screen.set_property",
            "Sets a property of a screen node through the screen panel's undo history (history.undo with context = the screen id). " +
            "node = node id or unique node name; property = a property the inspector lists as editable for the node's control type. " +
            "Values: integers, finite numbers, true/false, text of 4096 characters at most (never starting with '{': no markup or binding), " +
            "asset ids of an accepted type for image properties, null to clear.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"node\":{\"type\":\"string\"},\"property\":{\"type\":\"string\"},\"value\":{\"type\":[\"string\",\"number\",\"boolean\",\"null\"]}},\"required\":[\"asset\",\"node\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                if (host.IsPlaySessionActive)
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
                }

                RemoteAssetReference asset = OpenScreen(call, host);
                UIScreenDocument document = RequireDocument(host, asset);
                UIScreenNode node = FindNode(document, call.GetString("node", 128));
                string property = call.GetString("property", 128);
                string value = call.HasArgument("value") ? ToSerializedValue(call.GetArgument("value")) : null;
                string error = Validate(node.ControlType, property, value);
                if (error != null)
                {
                    throw new EditorRemoteArgumentException(error);
                }

                if (!host.TrySetScreenProperty(asset.Info.Id, node, property, value, out error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The property was not set.");
                }

                call.Succeed(new { asset_id = asset.Info.Id, node_id = node.Id.Value, property, value });
            }));
    }

    /// <summary>
    /// The rules of plan P12 for <c>screen.set_property</c>: the property is an editable descriptor of the control type
    /// (never a name with '{', ':' or '.', so no attached property or markup), the value matches the descriptor's type
    /// (integers, finite numbers, True/False, an accepted catalogued asset), is 4096 characters at most and never starts
    /// with '{'. A null value clears the property. Null when allowed, otherwise the reason.
    /// </summary>
    public static string Validate(string controlType, string property, string value)
    {
        if (string.IsNullOrWhiteSpace(property) || property.IndexOfAny(new[] { '{', ':', '.' }) >= 0)
        {
            return $"'{property}' is not a property name the remote control may set.";
        }

        UIPropertyDescriptor descriptor = FindDescriptor(controlType, property);
        if (descriptor == null)
        {
            return $"'{property}' is not an inspector property of '{controlType}'.";
        }

        if (!descriptor.IsEditable)
        {
            return $"'{property}' is read-only.";
        }

        if (value == null)
        {
            return null;
        }

        if (value.Length > EditorRemoteValidation.MaxPropertyTextLength)
        {
            return $"A value is limited to {EditorRemoteValidation.MaxPropertyTextLength} characters.";
        }

        if (value.TrimStart().StartsWith('{'))
        {
            return "A value cannot start with '{': markup extensions and bindings are not set remotely.";
        }

        Type type = Nullable.GetUnderlyingType(descriptor.ValueType) ?? descriptor.ValueType;
        if (type == typeof(int))
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : $"'{property}' expects an integer.";
        }

        if (type == typeof(float))
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && float.IsFinite(number)
                ? null
                : $"'{property}' expects a finite number.";
        }

        if (type == typeof(bool))
        {
            return value is "True" or "False" ? null : $"'{property}' expects true or false.";
        }

        if (descriptor.IsAssetReference)
        {
            if (!Guid.TryParse(value, out Guid assetId))
            {
                return $"'{property}' expects an asset id ({string.Join(", ", descriptor.AssetTypes!)}).";
            }

            AssetInfo info = AssetCatalog.Get(assetId);
            return descriptor.AcceptsAsset(info) ? null : $"{assetId} is not an asset of the catalog of type {string.Join(", ", descriptor.AssetTypes!)}.";
        }

        return null;
    }

    /// <summary>The text the inspector would store: numbers as written, booleans as True/False, an asset id in the 'D' form, null to clear.</summary>
    public static string ToSerializedValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Number:
                return value.GetRawText();
            case JsonValueKind.True:
                return "True";
            case JsonValueKind.False:
                return "False";
            case JsonValueKind.String:
                string text = value.GetString();
                if (text.Length > EditorRemoteValidation.MaxPropertyTextLength)
                {
                    throw new EditorRemoteArgumentException($"A value is limited to {EditorRemoteValidation.MaxPropertyTextLength} characters.");
                }

                // As the inspector: an empty text clears the property, a guid is stored in its 'D' form.
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return Guid.TryParse(text.Trim(), out Guid id) ? id.ToString("D") : text;
            default:
                throw new EditorRemoteArgumentException("A value is a string, a number, true/false or null.");
        }
    }

    private static UIPropertyDescriptor FindDescriptor(string controlType, string property)
    {
        IReadOnlyList<UIPropertyDescriptor> descriptors = UIPropertyRegistry.Default.GetDescriptors(controlType);
        for (int i = 0; i < descriptors.Count; i++)
        {
            if (string.Equals(descriptors[i].Name, property, StringComparison.Ordinal))
            {
                return descriptors[i];
            }
        }

        return null;
    }

    /// <summary>A node by id, otherwise by name; a name shared by several nodes is refused.</summary>
    private static UIScreenNode FindNode(UIScreenDocument document, string reference)
    {
        if (Guid.TryParse(reference, out Guid id))
        {
            return document.FindNode(new DocumentNodeId(id))
                ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"The screen has no node {id}.");
        }

        var matches = new List<UIScreenNode>();
        if (document.Root != null)
        {
            CollectByName(document.Root, reference, matches);
        }

        return matches.Count switch
        {
            0 => throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"The screen has no node named '{reference}'."),
            1 => matches[0],
            _ => throw new EditorRemoteArgumentException($"{matches.Count} nodes are named '{reference}': pass a node id (screen.get)."),
        };
    }

    private static void CollectByName(UIScreenNode node, string name, List<UIScreenNode> matches)
    {
        if (string.Equals(node.Name, name, StringComparison.Ordinal))
        {
            matches.Add(node);
        }

        foreach (UIScreenNode child in node.Children)
        {
            CollectByName(child, name, matches);
        }
    }

    /// <summary>Adds the node and its subtree depth first; false when <see cref="MaxNodes"/> cut it.</summary>
    private static bool Describe(UIScreenNode node, Guid? parentId, List<RemoteScreenNode> nodes)
    {
        if (nodes.Count >= MaxNodes)
        {
            return false;
        }

        IReadOnlyList<UIPropertyDescriptor> descriptors = UIPropertyRegistry.Default.GetDescriptors(node.ControlType);
        var properties = new List<RemoteScreenProperty>(descriptors.Count);
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (UIPropertyDescriptor descriptor in descriptors)
        {
            known.Add(descriptor.Name);
            string value = node.TryGetProperty(descriptor.Name, out UIScreenPropertyValue set) ? set?.EffectiveSerializedValue : null;
            properties.Add(new RemoteScreenProperty(descriptor.Name, TypeName(descriptor), value, descriptor.IsEditable, descriptor.AssetTypes));
        }

        var others = new List<string>();
        foreach (string name in node.Properties.Keys)
        {
            if (!known.Contains(name))
            {
                others.Add(name);
            }
        }

        others.Sort(StringComparer.Ordinal);
        nodes.Add(new RemoteScreenNode(node.Id.Value, parentId, node.Name, node.ControlType, properties, others));
        foreach (UIScreenNode child in node.Children)
        {
            if (!Describe(child, node.Id.Value, nodes))
            {
                return false;
            }
        }

        return true;
    }

    private static string TypeName(UIPropertyDescriptor descriptor)
    {
        if (descriptor.IsAssetReference)
        {
            return "asset";
        }

        Type type = Nullable.GetUnderlyingType(descriptor.ValueType) ?? descriptor.ValueType;
        return type == typeof(int) ? "integer" : type == typeof(float) ? "number" : type == typeof(bool) ? "boolean" : "string";
    }

    private static RemoteAssetReference OpenScreen(EditorRemoteCall call, IEditorRemoteHost host)
    {
        RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.UIScreen);
        if (host.OpenAsset(asset.FullPath, asset.Type) == null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' could not be opened: see logs.read.");
        }

        return asset;
    }

    private static UIScreenDocument RequireDocument(IEditorRemoteHost host, RemoteAssetReference asset)
        => host.GetScreenDocument(asset.Info.Id)
            ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' has no document loaded: see logs.read.");
}
