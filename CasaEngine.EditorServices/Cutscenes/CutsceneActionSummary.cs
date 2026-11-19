using System.Globalization;
using CasaEngine.Framework.Cutscenes;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>A one-line description of an action for the action tree, such as <c>Wait  1.5 s</c>.</summary>
public static class CutsceneActionSummary
{
    private const string None = "(none)";
    private const string Unknown = "?";

    public static string Describe(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return action switch
        {
            WaitCutsceneActionData wait => $"{action.Type}  {Number(wait.Seconds)} s",
            WaitFramesCutsceneActionData waitFrames => $"{action.Type}  {Number(waitFrames.Frames)} frames",
            MoveToCutsceneActionData moveTo => $"{action.Type}  {Text(moveTo.EntityName)} → {Vector(moveTo.Destination)}",
            NavigateToCutsceneActionData navigateTo => $"{action.Type}  {Text(navigateTo.EntityName)} → {Vector(navigateTo.Destination)}",
            SequenceCutsceneActionData sequence => $"{action.Type}  {Count(sequence.Actions.Count)}",
            ParallelCutsceneActionData parallel => $"{action.Type}  {Count(parallel.Actions.Count)}",
            AtCutsceneActionData at => $"{action.Type}  {Number(at.Seconds)} s",
            RepeatCutsceneActionData repeat => repeat.Count is 0 ? $"{action.Type}  forever" : $"{action.Type}  ×{Number(repeat.Count)}",
            PlaySoundCutsceneActionData playSound => $"{action.Type}  {ShortId(playSound.SoundAssetId)}  vol {Number(playSound.Volume)}",
            PlayMusicCutsceneActionData playMusic => $"{action.Type}  {ShortId(playMusic.SoundAssetId)}  fade in {Number(playMusic.FadeInSeconds)} s",
            StopMusicCutsceneActionData stopMusic => $"{action.Type}  fade out {Number(stopMusic.FadeOutSeconds)} s",
            FadeMusicCutsceneActionData fadeMusic => $"{action.Type}  → {Number(fadeMusic.TargetVolume)} over {Number(fadeMusic.DurationSeconds)} s",
            FadeScreenCutsceneActionData fadeScreen => $"{action.Type}  → #{fadeScreen.R:X2}{fadeScreen.G:X2}{fadeScreen.B:X2} over {Number(fadeScreen.DurationSeconds)} s",
            FadeScreenFramesCutsceneActionData fadeFrames => $"{action.Type}  {Hex(fadeFrames.From)} → {Hex(fadeFrames.To)}  {Number(fadeFrames.Frames)} frames",
            OpenScreenCutsceneActionData openScreen => $"{action.Type}  {Text(openScreen.Target)}",
            PlayVideoCutsceneActionData playVideo => $"{action.Type}  {Text(playVideo.Target)}",
            ChangeWorldCutsceneActionData changeWorld => $"{action.Type}  {Text(changeWorld.Target)}",
            ShowSpriteCutsceneActionData showSprite => showSprite.Sprite == null
                ? $"{action.Type}  {Text(showSprite.EntityName)}"
                : $"{action.Type}  {Text(showSprite.EntityName)} ({showSprite.Sprite})",
            HideSpriteCutsceneActionData hideSprite => $"{action.Type}  {Text(hideSprite.EntityName)}",
            PlayVideoSpriteCutsceneActionData playVideoSprite => $"{action.Type}  {Text(playVideoSprite.EntityName)}",
            StartVideoSpriteCutsceneActionData startVideoSprite => $"{action.Type}  {Text(startVideoSprite.EntityName)}",
            MoveSpriteCutsceneActionData moveSprite => $"{action.Type}  {Text(moveSprite.EntityName)} → ({Number(moveSprite.X)}, {Number(moveSprite.Y)}) @ {Number(moveSprite.Speed)}",
            TintSpriteCutsceneActionData tintSprite => $"{action.Type}  {Text(tintSprite.EntityName)} → {Hex(tintSprite.Color)} over {Number(tintSprite.Duration)} s",
            ExitOnInputCutsceneActionData exitOnInput => $"{action.Type}  {Text(exitOnInput.Mapping)}: {(exitOnInput.Inputs.Count == 0 ? None : string.Join(", ", exitOnInput.Inputs))} → {Text(exitOnInput.NextCutscene)}",
            UnknownCutsceneActionData => $"{action.Type}  (unknown type)",
            _ => action.Type,
        };
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Number(float? value) => value is { } number ? Number(number) : Unknown;

    private static string Number(int? value) => value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Unknown;

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? None : value;

    private static string Vector(Vector3 value) => $"({Number(value.X)}, {Number(value.Y)}, {Number(value.Z)})";

    private static string Count(int count) => count == 1 ? "1 action" : $"{count.ToString(CultureInfo.InvariantCulture)} actions";

    private static string ShortId(Guid id) => id == Guid.Empty ? None : id.ToString("N")[..8];

    private static string Hex(Color? color)
        => color is { } value
            ? value.A == 255
                ? $"#{value.R:X2}{value.G:X2}{value.B:X2}"
                : $"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}"
            : Unknown;
}
