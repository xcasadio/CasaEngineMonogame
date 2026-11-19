using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Finds what the loader dropped or rewrote without a format error (ADR-0134): the JSON of the file is compared with the
/// JSON a save would write at once. For each known key of an action present in the file, the save must write the same
/// key, of the same kind and of the same value, and each <c>actions</c> array must keep its number of entries. Unknown
/// keys and the default values a save adds are accepted, as they are part of the canonical form.
/// </summary>
public static class CutsceneLoadFidelity
{
    /// <summary>The losses of a file that was loaded into an asset, in the order of the tree.</summary>
    public static IReadOnlyList<CutsceneLoadLoss> FindLosses(JObject source, CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(asset);

        var losses = new List<CutsceneLoadLoss>();
        if (source["root_action"] is not { } sourceRoot)
        {
            return losses;
        }

        var saved = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, saved);

        if (sourceRoot is not JObject sourceRootNode)
        {
            losses.Add(new CutsceneLoadLoss(null, "root_action", "root_action", $"root_action must be an object, but is {Describe(sourceRoot)}: a save leaves it out."));
        }
        else if (asset.RootAction != null && saved["root_action"] is JObject savedRootNode)
        {
            CompareAction(sourceRootNode, savedRootNode, asset.RootAction, "root_action", losses);
        }

        return losses;
    }

    /// <summary>The children of a Sequence, a Parallel, a Repeat or an At; null for the other actions.</summary>
    internal static List<CutsceneActionData>? GetChildren(CutsceneActionData action)
        => action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            ParallelCutsceneActionData parallel => parallel.Actions,
            RepeatCutsceneActionData repeat => repeat.Actions,
            AtCutsceneActionData at => at.Actions,
            _ => null,
        };

    private static void CompareAction(JObject source, JObject saved, CutsceneActionData action, string path, List<CutsceneLoadLoss> losses)
    {
        var properties = CutsceneActionPropertyEditor.GetProperties(action);
        for (int index = 0; index < properties.Count; index++)
        {
            string key = properties[index].Key;
            if (source[key] is not { } sourceValue)
            {
                continue;
            }

            string? difference = Compare(sourceValue, saved[key]);
            if (difference != null)
            {
                losses.Add(new CutsceneLoadLoss(action, key, path, $"{key} {difference}"));
            }
        }

        var children = GetChildren(action);
        if (children == null || source["actions"] is not { } sourceActions)
        {
            return;
        }

        if (sourceActions is not JArray sourceArray)
        {
            losses.Add(new CutsceneLoadLoss(action, "actions", path, $"actions must be an array, but is {Describe(sourceActions)}: a save writes an empty list."));
            return;
        }

        if (sourceArray.Count != children.Count)
        {
            losses.Add(new CutsceneLoadLoss(action, "actions", path, $"actions holds {sourceArray.Count} entries, but a save writes {children.Count}: an entry that is not an object is skipped."));
        }

        var savedArray = saved["actions"] as JArray;
        int child = 0;
        for (int index = 0; index < sourceArray.Count; index++)
        {
            if (sourceArray[index] is not JObject sourceChild)
            {
                continue;
            }

            if (child < children.Count && savedArray != null && child < savedArray.Count && savedArray[child] is JObject savedChild)
            {
                CompareAction(sourceChild, savedChild, children[child], $"{path}.actions[{child}]", losses);
            }

            child++;
        }
    }

    /// <summary>What differs between a value of the file and what a save writes; null when nothing does.</summary>
    private static string? Compare(JToken source, JToken? saved)
    {
        if (source.Type == JTokenType.Null)
        {
            return saved is { Type: JTokenType.Null } ? null : $"is null in the file, but a save writes {(saved == null ? "nothing" : Describe(saved))}.";
        }

        if (saved == null)
        {
            return $"is {Describe(source)} in the file, but a save writes nothing.";
        }

        switch (source)
        {
            case JObject sourceObject:
                if (saved is not JObject savedObject)
                {
                    return $"is an object in the file, but a save writes {Describe(saved)}.";
                }

                foreach (var property in sourceObject.Properties())
                {
                    string? difference = Compare(property.Value, savedObject[property.Name]);
                    if (difference != null)
                    {
                        return $"{property.Name} {difference}";
                    }
                }

                return null;

            case JArray sourceArray:
                if (saved is not JArray savedArray)
                {
                    return $"is an array in the file, but a save writes {Describe(saved)}.";
                }

                if (savedArray.Count < sourceArray.Count)
                {
                    return $"holds {sourceArray.Count} entries in the file, but a save writes {savedArray.Count}.";
                }

                for (int index = 0; index < sourceArray.Count; index++)
                {
                    string? difference = Compare(sourceArray[index], savedArray[index]);
                    if (difference != null)
                    {
                        return $"[{index}] {difference}";
                    }
                }

                return null;

            default:
                return CompareValues(source, saved);
        }
    }

    private static string? CompareValues(JToken source, JToken saved)
    {
        string sourceKind = Kind(source);
        if (sourceKind != Kind(saved))
        {
            return $"is {Describe(source)} in the file, but a save writes {Describe(saved)}.";
        }

        bool same = sourceKind switch
        {
            "a number" => source.Value<float>() == saved.Value<float>(),
            "a string" => string.Equals(source.ToString(), saved.ToString(), StringComparison.OrdinalIgnoreCase),
            "a boolean" => source.Value<bool>() == saved.Value<bool>(),
            _ => JToken.DeepEquals(source, saved),
        };

        return same ? null : $"is {Describe(source)} in the file, but a save writes {Describe(saved)}.";
    }

    private static string Kind(JToken token)
        => token.Type switch
        {
            JTokenType.Integer or JTokenType.Float => "a number",
            JTokenType.String or JTokenType.Guid => "a string",
            JTokenType.Boolean => "a boolean",
            JTokenType.Object => "an object",
            JTokenType.Array => "an array",
            JTokenType.Null => "null",
            _ => token.Type.ToString(),
        };

    private static string Describe(JToken token)
        => token is JValue ? token.ToString(Newtonsoft.Json.Formatting.None) : Kind(token);
}
