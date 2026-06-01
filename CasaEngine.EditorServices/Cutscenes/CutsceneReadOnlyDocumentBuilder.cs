using System.Globalization;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.EditorServices.Cutscenes;

public static class CutsceneReadOnlyDocumentBuilder
{
    public static CutsceneReadOnlyDocument Build(CutsceneAsset asset, CutsceneDebugSnapshot? runtimeSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var validationResult = asset.Validate();
        return new CutsceneReadOnlyDocument(
            string.IsNullOrWhiteSpace(asset.Name) ? "<unnamed cutscene>" : asset.Name,
            asset.FileName ?? string.Empty,
            asset.RootAction == null ? null : BuildAction(asset.RootAction, "root_action"),
            CopyValidationMessages(validationResult.Messages),
            runtimeSnapshot?.State ?? CutsceneRuntimeState.Idle,
            runtimeSnapshot == null ? Array.Empty<CutsceneReadOnlyCoroutineInfo>() : CopyActiveCoroutines(runtimeSnapshot.ActiveCoroutines),
            runtimeSnapshot?.ActiveActionType,
            runtimeSnapshot?.ActiveActionEntityName,
            runtimeSnapshot?.ActiveActionDestination,
            runtimeSnapshot?.ActiveActionState,
            runtimeSnapshot?.ActiveActionStopReason);
    }

    private static CutsceneReadOnlyActionNode BuildAction(CutsceneActionData action, string path)
    {
        var node = new CutsceneReadOnlyActionNode(action.Type, path);

        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                node.AddProperty("seconds", waitAction.Seconds.ToString("0.###", CultureInfo.InvariantCulture));
                break;

            case MoveToCutsceneActionData moveToAction:
                node.AddProperty("entity", moveToAction.EntityName);
                node.AddProperty("destination", FormatVector3(moveToAction.Destination));
                node.AddProperty("stopping_distance", moveToAction.StoppingDistance.ToString("0.###", CultureInfo.InvariantCulture));
                node.AddProperty("timeout_seconds", moveToAction.TimeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                break;

            case NavigateToCutsceneActionData navigateToAction:
                node.AddProperty("entity", navigateToAction.EntityName);
                node.AddProperty("destination", FormatVector3(navigateToAction.Destination));
                node.AddProperty("stopping_distance", navigateToAction.StoppingDistance.ToString("0.###", CultureInfo.InvariantCulture));
                node.AddProperty("timeout_seconds", navigateToAction.TimeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                break;

            case SequenceCutsceneActionData sequenceAction:
                node.AddProperty("children", sequenceAction.Actions.Count.ToString(CultureInfo.InvariantCulture));
                AddChildren(node, sequenceAction.Actions, path);
                break;

            case ParallelCutsceneActionData parallelAction:
                node.AddProperty("children", parallelAction.Actions.Count.ToString(CultureInfo.InvariantCulture));
                AddChildren(node, parallelAction.Actions, path);
                break;

            case UnknownCutsceneActionData unknownAction:
                node.AddProperty("unknown_type", unknownAction.UnknownType);
                break;

            default:
                node.AddProperty("runtime_type", action.GetType().FullName ?? action.GetType().Name);
                break;
        }

        return node;
    }

    private static void AddChildren(CutsceneReadOnlyActionNode node, List<CutsceneActionData> actions, string path)
    {
        for (int index = 0; index < actions.Count; index++)
        {
            node.AddChild(BuildAction(actions[index], $"{path}.actions[{index}]"));
        }
    }

    private static string FormatVector3(Microsoft.Xna.Framework.Vector3 value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}");
    }

    private static IReadOnlyList<CutsceneValidationMessage> CopyValidationMessages(IReadOnlyList<CutsceneValidationMessage> messages)
    {
        if (messages.Count == 0)
        {
            return Array.Empty<CutsceneValidationMessage>();
        }

        var copy = new CutsceneValidationMessage[messages.Count];
        for (int index = 0; index < messages.Count; index++)
        {
            copy[index] = messages[index];
        }

        return copy;
    }

    private static IReadOnlyList<CutsceneReadOnlyCoroutineInfo> CopyActiveCoroutines(IReadOnlyList<CoroutineDebugInfo> activeCoroutines)
    {
        if (activeCoroutines.Count == 0)
        {
            return Array.Empty<CutsceneReadOnlyCoroutineInfo>();
        }

        var copy = new CutsceneReadOnlyCoroutineInfo[activeCoroutines.Count];
        for (int index = 0; index < activeCoroutines.Count; index++)
        {
            var coroutine = activeCoroutines[index];
            copy[index] = new CutsceneReadOnlyCoroutineInfo(
                coroutine.Id,
                coroutine.Name,
                coroutine.OwnerName,
                coroutine.CurrentInstruction,
                coroutine.State,
                coroutine.IsPaused,
                coroutine.RemainingTime);
        }

        return copy;
    }
}