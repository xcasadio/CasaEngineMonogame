using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Decides whether a cutscene can be saved without destroying data silently (ADR-0095). A save is refused when the tree
/// holds an action of an unknown type, a format error or a load loss, and when the JSON about to be written would be
/// read back by the loader with one of these: the editor never writes a file that its own loader would reject.
/// </summary>
public static class CutsceneSaveGuard
{
    /// <summary>
    /// True when the save is refused; <paramref name="reason"/> then names the path and the key of the first case.
    /// </summary>
    public static bool TryGetBlockingReason(CutsceneEditSession session, out string reason)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (TryFindProblem(session.Asset, session.LoadLosses, out reason))
        {
            return true;
        }

        var saved = new JObject();
        CutsceneAssetJsonSerializer.Save(session.Asset, saved);
        var reloaded = CutsceneEditSession.Load(saved);
        if (TryFindProblem(reloaded.Asset, reloaded.LoadLosses, out string reloadProblem))
        {
            reason = $"A save would write a file that loads back with a problem: {reloadProblem}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryFindProblem(CutsceneAsset asset, IReadOnlyList<CutsceneLoadLoss> losses, out string reason)
    {
        if (asset.RootAction != null && TryFindTreeProblem(asset.RootAction, "root_action", out reason))
        {
            return true;
        }

        if (losses.Count > 0)
        {
            var loss = losses[0];
            reason = $"{loss.Path} ({loss.Key}): {loss.Message}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryFindTreeProblem(CutsceneActionData action, string path, out string reason)
    {
        if (action is UnknownCutsceneActionData unknown)
        {
            reason = $"{path} (type): the action type '{unknown.UnknownType}' is unknown.";
            return true;
        }

        var formatErrors = CutsceneActionPropertyEditor.GetFormatErrors(action);
        if (formatErrors is { Count: > 0 })
        {
            var properties = CutsceneActionPropertyEditor.GetProperties(action);
            for (int index = 0; index < properties.Count; index++)
            {
                if (formatErrors.TryGetValue(properties[index].Key, out string? message))
                {
                    reason = $"{path} ({properties[index].Key}): {message}";
                    return true;
                }
            }

            var first = formatErrors.First();
            reason = $"{path} ({first.Key}): {first.Value}";
            return true;
        }

        var children = CutsceneLoadFidelity.GetChildren(action);
        if (children != null)
        {
            for (int index = 0; index < children.Count; index++)
            {
                if (TryFindTreeProblem(children[index], $"{path}.actions[{index}]", out reason))
                {
                    return true;
                }
            }
        }

        reason = string.Empty;
        return false;
    }
}
