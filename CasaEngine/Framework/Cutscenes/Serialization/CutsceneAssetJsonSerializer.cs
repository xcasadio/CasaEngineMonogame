using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering.Depth;
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

            case PlaySoundCutsceneActionData playSoundAction:
                node["sound_asset_id"] = playSoundAction.SoundAssetId.ToString();
                node["volume"] = playSoundAction.Volume;
                node["bus_name"] = playSoundAction.BusName;
                break;

            case PlayMusicCutsceneActionData playMusicAction:
                node["sound_asset_id"] = playMusicAction.SoundAssetId.ToString();
                node["fade_in_seconds"] = playMusicAction.FadeInSeconds;
                node["crossfade"] = playMusicAction.Crossfade;
                break;

            case StopMusicCutsceneActionData stopMusicAction:
                node["fade_out_seconds"] = stopMusicAction.FadeOutSeconds;
                break;

            case FadeMusicCutsceneActionData fadeMusicAction:
                node["target_volume"] = fadeMusicAction.TargetVolume;
                node["duration_seconds"] = fadeMusicAction.DurationSeconds;
                break;

            case FadeScreenCutsceneActionData fadeScreenAction:
                node["r"] = fadeScreenAction.R;
                node["g"] = fadeScreenAction.G;
                node["b"] = fadeScreenAction.B;
                node["duration_seconds"] = fadeScreenAction.DurationSeconds;
                node["blend_mode"] = fadeScreenAction.BlendMode.ToString();
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
            CutsceneActionTypes.PlaySound => new PlaySoundCutsceneActionData
            {
                SoundAssetId = node["sound_asset_id"] is { } playSoundIdNode ? playSoundIdNode.GetGuid() : Guid.Empty,
                Volume = node["volume"]?.GetSingle() ?? 1f,
                BusName = node["bus_name"]?.GetString() ?? string.Empty,
            },
            CutsceneActionTypes.PlayMusic => new PlayMusicCutsceneActionData
            {
                SoundAssetId = node["sound_asset_id"] is { } playMusicIdNode ? playMusicIdNode.GetGuid() : Guid.Empty,
                FadeInSeconds = node["fade_in_seconds"]?.GetSingle() ?? 0f,
                Crossfade = node["crossfade"]?.GetBoolean() ?? true,
            },
            CutsceneActionTypes.StopMusic => new StopMusicCutsceneActionData
            {
                FadeOutSeconds = node["fade_out_seconds"]?.GetSingle() ?? 0f,
            },
            CutsceneActionTypes.FadeMusic => new FadeMusicCutsceneActionData
            {
                TargetVolume = node["target_volume"]?.GetSingle() ?? 0f,
                DurationSeconds = node["duration_seconds"]?.GetSingle() ?? 0f,
            },
            CutsceneActionTypes.FadeScreen => new FadeScreenCutsceneActionData
            {
                R = node["r"]?.GetByte() ?? 0,
                G = node["g"]?.GetByte() ?? 0,
                B = node["b"]?.GetByte() ?? 0,
                DurationSeconds = node["duration_seconds"]?.GetSingle() ?? 0f,
                BlendMode = ParseBlendMode(node["blend_mode"]),
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

    private static SpriteBlendMode ParseBlendMode(JToken node)
    {
        var text = node?.GetString();
        return !string.IsNullOrEmpty(text) && Enum.TryParse<SpriteBlendMode>(text, out var blendMode)
            ? blendMode
            : SpriteBlendMode.Additive;
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