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

    private static void ValidateAction(CutsceneActionData action, string path, CutsceneValidationResult result)
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

            case MoveToCutsceneActionData moveToAction:
                if (string.IsNullOrWhiteSpace(moveToAction.EntityName))
                {
                    result.AddError(path, "MoveTo.entity is required.");
                }

                if (moveToAction.StoppingDistance < 0f)
                {
                    result.AddError(path, "MoveTo.stopping_distance must be greater than or equal to zero.");
                }

                if (moveToAction.TimeoutSeconds < 0f)
                {
                    result.AddError(path, "MoveTo.timeout_seconds must be greater than or equal to zero.");
                }

                break;

            case NavigateToCutsceneActionData navigateToAction:
                if (string.IsNullOrWhiteSpace(navigateToAction.EntityName))
                {
                    result.AddError(path, "NavigateTo.entity is required.");
                }

                if (navigateToAction.StoppingDistance < 0f)
                {
                    result.AddError(path, "NavigateTo.stopping_distance must be greater than or equal to zero.");
                }

                if (navigateToAction.TimeoutSeconds < 0f)
                {
                    result.AddError(path, "NavigateTo.timeout_seconds must be greater than or equal to zero.");
                }

                break;

            case PlaySoundCutsceneActionData playSoundAction:
                if (playSoundAction.SoundAssetId == Guid.Empty)
                {
                    result.AddError(path, "PlaySound.sound_asset_id is required.");
                }

                if (playSoundAction.Volume is < 0f or > 1f)
                {
                    result.AddError(path, "PlaySound.volume must be between zero and one.");
                }

                break;

            case PlayMusicCutsceneActionData playMusicAction:
                if (playMusicAction.SoundAssetId == Guid.Empty)
                {
                    result.AddError(path, "PlayMusic.sound_asset_id is required.");
                }

                if (playMusicAction.FadeInSeconds < 0f)
                {
                    result.AddError(path, "PlayMusic.fade_in_seconds must be greater than or equal to zero.");
                }

                break;

            case StopMusicCutsceneActionData stopMusicAction:
                if (stopMusicAction.FadeOutSeconds < 0f)
                {
                    result.AddError(path, "StopMusic.fade_out_seconds must be greater than or equal to zero.");
                }

                break;

            case FadeMusicCutsceneActionData fadeMusicAction:
                if (fadeMusicAction.TargetVolume is < 0f or > 1f)
                {
                    result.AddError(path, "FadeMusic.target_volume must be between zero and one.");
                }

                if (fadeMusicAction.DurationSeconds < 0f)
                {
                    result.AddError(path, "FadeMusic.duration_seconds must be greater than or equal to zero.");
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