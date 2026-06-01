using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Cutscenes.Serialization;

public static class CutsceneAssetJsonSerializer
{
    public static void Save(CutsceneAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        node["id"] = asset.Id.ToString();
        node["name"] = asset.Name;
        node["type"] = nameof(CutsceneAsset);
        node["version"] = asset.Version;
        node["schema_version"] = CutsceneAsset.CurrentVersion;

        if (asset.RootAction != null)
        {
            node["root_action"] = SaveAction(asset.RootAction);
        }
    }

    public static void Load(CutsceneAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        int version = node["version"]?.GetInt32() ?? CutsceneAsset.CurrentVersion;
        if (!CanMigrate(version))
        {
            throw new InvalidOperationException($"Cutscene asset version {version} is newer than supported version {CutsceneAsset.CurrentVersion}.");
        }

        asset.Version = CutsceneAsset.CurrentVersion;
        asset.RootAction = node["root_action"] is JObject rootActionNode
            ? LoadAction(rootActionNode)
            : null;
    }

    public static bool CanMigrate(int version)
        => version > 0 && version <= CutsceneAsset.CurrentVersion;

    private static JObject SaveAction(CutsceneActionData action)
    {
        var node = new JObject
        {
            ["type"] = action.Type
        };

        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                node["seconds"] = waitAction.Seconds;
                break;

            case MoveToCutsceneActionData moveToAction:
                node["entity"] = moveToAction.EntityName;
                node["destination"] = SaveVector3(moveToAction.Destination);
                node["stopping_distance"] = moveToAction.StoppingDistance;
                node["timeout_seconds"] = moveToAction.TimeoutSeconds;
                break;

            case NavigateToCutsceneActionData navigateToAction:
                node["entity"] = navigateToAction.EntityName;
                node["destination"] = SaveVector3(navigateToAction.Destination);
                node["stopping_distance"] = navigateToAction.StoppingDistance;
                node["timeout_seconds"] = navigateToAction.TimeoutSeconds;
                break;

            case SequenceCutsceneActionData sequenceAction:
                node["actions"] = SaveActions(sequenceAction.Actions);
                break;

            case ParallelCutsceneActionData parallelAction:
                node["actions"] = SaveActions(parallelAction.Actions);
                break;
        }

        return node;
    }

    private static JArray SaveActions(List<CutsceneActionData> actions)
    {
        var actionsNode = new JArray();
        for (int index = 0; index < actions.Count; index++)
        {
            actionsNode.Add(SaveAction(actions[index]));
        }

        return actionsNode;
    }

    private static CutsceneActionData LoadAction(JObject node)
    {
        string actionType = node["type"]?.GetString() ?? string.Empty;
        return actionType switch
        {
            CutsceneActionTypes.Wait => new WaitCutsceneActionData
            {
                Seconds = node["seconds"]?.GetSingle() ?? 0f
            },
            CutsceneActionTypes.MoveTo => new MoveToCutsceneActionData
            {
                EntityName = node["entity"]?.GetString() ?? string.Empty,
                Destination = node["destination"] is { } destinationNode ? destinationNode.GetVector3() : Vector3.Zero,
                StoppingDistance = node["stopping_distance"]?.GetSingle() ?? 0.1f,
                TimeoutSeconds = node["timeout_seconds"]?.GetSingle() ?? 0f,
            },
            CutsceneActionTypes.NavigateTo => new NavigateToCutsceneActionData
            {
                EntityName = node["entity"]?.GetString() ?? string.Empty,
                Destination = node["destination"] is { } destinationNode ? destinationNode.GetVector3() : Vector3.Zero,
                StoppingDistance = node["stopping_distance"]?.GetSingle() ?? 0.1f,
                TimeoutSeconds = node["timeout_seconds"]?.GetSingle() ?? 0f,
            },
            CutsceneActionTypes.Sequence => LoadSequence(node),
            CutsceneActionTypes.Parallel => LoadParallel(node),
            _ => new UnknownCutsceneActionData(actionType)
        };
    }

    private static SequenceCutsceneActionData LoadSequence(JObject node)
    {
        var action = new SequenceCutsceneActionData();
        LoadActions(node, action.Actions);
        return action;
    }

    private static ParallelCutsceneActionData LoadParallel(JObject node)
    {
        var action = new ParallelCutsceneActionData();
        LoadActions(node, action.Actions);
        return action;
    }

    private static void LoadActions(JObject node, List<CutsceneActionData> actions)
    {
        if (node["actions"] is not JArray actionsNode)
        {
            return;
        }

        for (int index = 0; index < actionsNode.Count; index++)
        {
            if (actionsNode[index] is JObject actionNode)
            {
                actions.Add(LoadAction(actionNode));
            }
        }
    }

    private static JObject SaveVector3(Vector3 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };
    }
}