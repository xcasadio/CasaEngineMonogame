namespace CasaEngine.Framework.Cutscenes;

public static class CutsceneValidator
{
    public static CutsceneValidationResult Validate(CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var result = new CutsceneValidationResult();
        if (asset.RootAction == null)
        {
            result.AddError("root_action", "RootAction is required.");
            return result;
        }

        ValidateAction(asset.RootAction, "root_action", result);
        return result;
    }

    private static void ValidateAction(CutsceneActionData? action, string path, CutsceneValidationResult result)
    {
        if (action == null)
        {
            result.AddError(path, "Action is required.");
            return;
        }

        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                if (waitAction.Seconds < 0f)
                {
                    result.AddError(path, "Wait.seconds must be greater than or equal to zero.");
                }

                break;

            case SequenceCutsceneActionData sequenceAction:
                ValidateActionList(sequenceAction.Actions, path, "Sequence", result);
                break;

            case ParallelCutsceneActionData parallelAction:
                ValidateActionList(parallelAction.Actions, path, "Parallel", result);
                break;

            case UnknownCutsceneActionData unknownAction:
                result.AddError(path, $"Unknown cutscene action type: {unknownAction.UnknownType}.");
                break;

            default:
                result.AddError(path, $"Unsupported cutscene action data type: {action.GetType().FullName}.");
                break;
        }
    }

    private static void ValidateActionList(List<CutsceneActionData> actions, string path, string actionName, CutsceneValidationResult result)
    {
        if (actions.Count == 0)
        {
            result.AddWarning(path, $"{actionName} action has no children.");
            return;
        }

        for (int index = 0; index < actions.Count; index++)
        {
            ValidateAction(actions[index], $"{path}.actions[{index}]", result);
        }
    }
}