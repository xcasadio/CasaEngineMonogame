using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// The editable properties of every cutscene action type. The source of truth is <c>SaveAction</c> in
/// <c>CutsceneAssetJsonSerializer</c>: each field it writes, under its JSON key, <c>type</c> and <c>actions</c> excepted.
/// The service does not touch <c>FormatErrors</c>; the command that sets a property does.
/// </summary>
public static class CutsceneActionPropertyEditor
{
    private static readonly CutsceneActionProperty[] NoProperties = [];

    /// <summary>
    /// The action types that have no editable property: their only field is the list of children, which is the
    /// structure of the tree. An action of an unknown type is exempt too (<see cref="IsExempt"/>).
    /// </summary>
    public static readonly IReadOnlyList<string> ExemptActionTypes = [CutsceneActionTypes.Sequence, CutsceneActionTypes.Parallel];

    private static readonly Dictionary<string, CutsceneActionProperty[]> PropertiesByType = BuildProperties();

    /// <summary>The editable properties of an action, in the order the serializer writes them.</summary>
    public static IReadOnlyList<CutsceneActionProperty> GetProperties(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is UnknownCutsceneActionData ? NoProperties : GetProperties(action.Type);
    }

    /// <summary>The editable properties of an action type; empty for an exempt or an unknown type.</summary>
    public static IReadOnlyList<CutsceneActionProperty> GetProperties(string actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        return PropertiesByType.TryGetValue(actionType, out var properties) ? properties : NoProperties;
    }

    /// <summary>True for the actions that have no editable property by design: Sequence, Parallel and unknown actions.</summary>
    public static bool IsExempt(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is UnknownCutsceneActionData || ExemptActionTypes.Contains(action.Type);
    }

    /// <summary>The property of an action under a JSON key, or null.</summary>
    public static CutsceneActionProperty? FindProperty(CutsceneActionData action, string key)
    {
        var properties = GetProperties(action);
        for (int index = 0; index < properties.Count; index++)
        {
            if (string.Equals(properties[index].Key, key, StringComparison.Ordinal))
            {
                return properties[index];
            }
        }

        return null;
    }

    /// <summary>
    /// The <c>FormatErrors</c> dictionary of the ten action classes that carry one, keyed by JSON key; null for the others.
    /// </summary>
    public static Dictionary<string, string>? GetFormatErrors(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            WaitFramesCutsceneActionData waitFrames => waitFrames.FormatErrors,
            FadeScreenFramesCutsceneActionData fadeScreenFrames => fadeScreenFrames.FormatErrors,
            ShowSpriteCutsceneActionData showSprite => showSprite.FormatErrors,
            HideSpriteCutsceneActionData hideSprite => hideSprite.FormatErrors,
            PlayVideoSpriteCutsceneActionData playVideoSprite => playVideoSprite.FormatErrors,
            StartVideoSpriteCutsceneActionData startVideoSprite => startVideoSprite.FormatErrors,
            MoveSpriteCutsceneActionData moveSprite => moveSprite.FormatErrors,
            TintSpriteCutsceneActionData tintSprite => tintSprite.FormatErrors,
            RepeatCutsceneActionData repeat => repeat.FormatErrors,
            ExitOnInputCutsceneActionData exitOnInput => exitOnInput.FormatErrors,
            _ => null,
        };
    }

    private static Dictionary<string, CutsceneActionProperty[]> BuildProperties()
    {
        return new Dictionary<string, CutsceneActionProperty[]>(StringComparer.Ordinal)
        {
            [CutsceneActionTypes.Wait] =
            [
                Number<WaitCutsceneActionData>("seconds", a => a.Seconds, (a, v) => a.Seconds = v).Within(0),
            ],
            [CutsceneActionTypes.MoveTo] =
            [
                Text<MoveToCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
                Vector<MoveToCutsceneActionData>("destination", a => a.Destination, (a, v) => a.Destination = v),
                Number<MoveToCutsceneActionData>("stopping_distance", a => a.StoppingDistance, (a, v) => a.StoppingDistance = v).Within(0),
                Number<MoveToCutsceneActionData>("timeout_seconds", a => a.TimeoutSeconds, (a, v) => a.TimeoutSeconds = v).Within(0),
            ],
            [CutsceneActionTypes.NavigateTo] =
            [
                Text<NavigateToCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
                Vector<NavigateToCutsceneActionData>("destination", a => a.Destination, (a, v) => a.Destination = v),
                Number<NavigateToCutsceneActionData>("stopping_distance", a => a.StoppingDistance, (a, v) => a.StoppingDistance = v).Within(0),
                Number<NavigateToCutsceneActionData>("timeout_seconds", a => a.TimeoutSeconds, (a, v) => a.TimeoutSeconds = v).Within(0),
            ],
            [CutsceneActionTypes.Repeat] =
            [
                NullableInteger<RepeatCutsceneActionData>("count", a => a.Count, (a, v) => a.Count = v).Within(0),
            ],
            [CutsceneActionTypes.PlaySound] =
            [
                Identifier<PlaySoundCutsceneActionData>("sound_asset_id", a => a.SoundAssetId, (a, v) => a.SoundAssetId = v).Means(CutsceneActionPropertySemantic.Sound),
                Number<PlaySoundCutsceneActionData>("volume", a => a.Volume, (a, v) => a.Volume = v).Within(0, 1),
                Text<PlaySoundCutsceneActionData>("bus_name", a => a.BusName, (a, v) => a.BusName = v),
            ],
            [CutsceneActionTypes.PlayMusic] =
            [
                Identifier<PlayMusicCutsceneActionData>("sound_asset_id", a => a.SoundAssetId, (a, v) => a.SoundAssetId = v).Means(CutsceneActionPropertySemantic.Sound),
                Number<PlayMusicCutsceneActionData>("fade_in_seconds", a => a.FadeInSeconds, (a, v) => a.FadeInSeconds = v).Within(0),
                Flag<PlayMusicCutsceneActionData>("crossfade", a => a.Crossfade, (a, v) => a.Crossfade = v),
            ],
            [CutsceneActionTypes.StopMusic] =
            [
                Number<StopMusicCutsceneActionData>("fade_out_seconds", a => a.FadeOutSeconds, (a, v) => a.FadeOutSeconds = v).Within(0),
            ],
            [CutsceneActionTypes.FadeMusic] =
            [
                Number<FadeMusicCutsceneActionData>("target_volume", a => a.TargetVolume, (a, v) => a.TargetVolume = v).Within(0, 1),
                Number<FadeMusicCutsceneActionData>("duration_seconds", a => a.DurationSeconds, (a, v) => a.DurationSeconds = v).Within(0),
            ],
            [CutsceneActionTypes.FadeScreen] =
            [
                Channel<FadeScreenCutsceneActionData>("r", a => a.R, (a, v) => a.R = v),
                Channel<FadeScreenCutsceneActionData>("g", a => a.G, (a, v) => a.G = v),
                Channel<FadeScreenCutsceneActionData>("b", a => a.B, (a, v) => a.B = v),
                Number<FadeScreenCutsceneActionData>("duration_seconds", a => a.DurationSeconds, (a, v) => a.DurationSeconds = v).Within(0),
                Choice<FadeScreenCutsceneActionData, SpriteBlendMode>("blend_mode", a => a.BlendMode, (a, v) => a.BlendMode = v),
            ],
            [CutsceneActionTypes.WaitFrames] =
            [
                NullableInteger<WaitFramesCutsceneActionData>("frames", a => a.Frames, (a, v) => a.Frames = v).Within(0),
            ],
            [CutsceneActionTypes.FadeScreenFrames] =
            [
                NullableColor<FadeScreenFramesCutsceneActionData>("from", a => a.From, (a, v) => a.From = v),
                NullableColor<FadeScreenFramesCutsceneActionData>("to", a => a.To, (a, v) => a.To = v),
                NullableInteger<FadeScreenFramesCutsceneActionData>("frames", a => a.Frames, (a, v) => a.Frames = v).Within(0),
                NullableChoice<FadeScreenFramesCutsceneActionData, ScreenEffectLayer>("layer", a => a.Layer, (a, v) => a.Layer = v),
                NullableChoice<FadeScreenFramesCutsceneActionData, SpriteBlendMode>("blend_mode", a => a.BlendMode, (a, v) => a.BlendMode = v),
            ],
            [CutsceneActionTypes.OpenScreen] =
            [
                Text<OpenScreenCutsceneActionData>("target", a => a.Target, (a, v) => a.Target = v).Means(CutsceneActionPropertySemantic.Screen),
            ],
            [CutsceneActionTypes.PlayVideo] =
            [
                Text<PlayVideoCutsceneActionData>("target", a => a.Target, (a, v) => a.Target = v).Means(CutsceneActionPropertySemantic.Screen),
            ],
            [CutsceneActionTypes.ChangeWorld] =
            [
                Text<ChangeWorldCutsceneActionData>("target", a => a.Target, (a, v) => a.Target = v).Means(CutsceneActionPropertySemantic.World),
            ],
            [CutsceneActionTypes.ShowSprite] =
            [
                Text<ShowSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
                Text<ShowSpriteCutsceneActionData>("sprite", a => a.Sprite, (a, v) => a.Sprite = v).Means(CutsceneActionPropertySemantic.Sprite),
                NullableNumber<ShowSpriteCutsceneActionData>("x", a => a.X, (a, v) => a.X = v),
                NullableNumber<ShowSpriteCutsceneActionData>("y", a => a.Y, (a, v) => a.Y = v),
                NullableColor<ShowSpriteCutsceneActionData>("color", a => a.Color, (a, v) => a.Color = v),
                NullableChoice<ShowSpriteCutsceneActionData, SpriteBlendMode>("blend_mode", a => a.BlendMode, (a, v) => a.BlendMode = v),
            ],
            [CutsceneActionTypes.HideSprite] =
            [
                Text<HideSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
            ],
            [CutsceneActionTypes.PlayVideoSprite] =
            [
                Text<PlayVideoSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
            ],
            [CutsceneActionTypes.StartVideoSprite] =
            [
                Text<StartVideoSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
            ],
            [CutsceneActionTypes.MoveSprite] =
            [
                Text<MoveSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
                NullableNumber<MoveSpriteCutsceneActionData>("x", a => a.X, (a, v) => a.X = v),
                NullableNumber<MoveSpriteCutsceneActionData>("y", a => a.Y, (a, v) => a.Y = v),
                NullableNumber<MoveSpriteCutsceneActionData>("speed", a => a.Speed, (a, v) => a.Speed = v).Within(0, null, true),
            ],
            [CutsceneActionTypes.TintSprite] =
            [
                Text<TintSpriteCutsceneActionData>("entity", a => a.EntityName, (a, v) => a.EntityName = v).Means(CutsceneActionPropertySemantic.Entity),
                NullableColor<TintSpriteCutsceneActionData>("color", a => a.Color, (a, v) => a.Color = v),
                NullableNumber<TintSpriteCutsceneActionData>("duration", a => a.Duration, (a, v) => a.Duration = v).Within(0),
            ],
            [CutsceneActionTypes.ExitOnInput] =
            [
                Text<ExitOnInputCutsceneActionData>("mapping", a => a.Mapping, (a, v) => a.Mapping = v).Means(CutsceneActionPropertySemantic.ButtonsMapping),
                TextList<ExitOnInputCutsceneActionData>("inputs", a => a.Inputs).Means(CutsceneActionPropertySemantic.InputList),
                Text<ExitOnInputCutsceneActionData>("next_cutscene", a => a.NextCutscene, (a, v) => a.NextCutscene = v).Means(CutsceneActionPropertySemantic.Cutscene),
            ],
        };
    }

    private static CutsceneActionProperty Number<TAction>(string key, Func<TAction, float> get, Action<TAction, float> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Number, false, typeof(float), a => get((TAction)a), (a, v) => set((TAction)a, (float)v!));

    private static CutsceneActionProperty NullableNumber<TAction>(string key, Func<TAction, float?> get, Action<TAction, float?> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Number, true, typeof(float), a => get((TAction)a), (a, v) => set((TAction)a, (float?)v));

    private static CutsceneActionProperty NullableInteger<TAction>(string key, Func<TAction, int?> get, Action<TAction, int?> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Integer, true, typeof(int), a => get((TAction)a), (a, v) => set((TAction)a, (int?)v));

    private static CutsceneActionProperty Channel<TAction>(string key, Func<TAction, byte> get, Action<TAction, byte> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Byte, false, typeof(byte), a => get((TAction)a), (a, v) => set((TAction)a, (byte)v!));

    private static CutsceneActionProperty Flag<TAction>(string key, Func<TAction, bool> get, Action<TAction, bool> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Boolean, false, typeof(bool), a => get((TAction)a), (a, v) => set((TAction)a, (bool)v!));

    private static CutsceneActionProperty Identifier<TAction>(string key, Func<TAction, Guid> get, Action<TAction, Guid> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Guid, false, typeof(Guid), a => get((TAction)a), (a, v) => set((TAction)a, (Guid)v!));

    private static CutsceneActionProperty NullableColor<TAction>(string key, Func<TAction, Color?> get, Action<TAction, Color?> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Color, true, typeof(Color), a => get((TAction)a), (a, v) => set((TAction)a, (Color?)v));

    private static CutsceneActionProperty Vector<TAction>(string key, Func<TAction, Vector3> get, Action<TAction, Vector3> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.Vector3, false, typeof(Vector3), a => get((TAction)a), (a, v) => set((TAction)a, (Vector3)v!));

    private static CutsceneActionProperty Choice<TAction, TEnum>(string key, Func<TAction, TEnum> get, Action<TAction, TEnum> set)
        where TAction : CutsceneActionData
        where TEnum : struct, Enum
        => new(key, CutsceneActionPropertyKind.Enum, false, typeof(TEnum), a => get((TAction)a), (a, v) => set((TAction)a, (TEnum)v!));

    private static CutsceneActionProperty NullableChoice<TAction, TEnum>(string key, Func<TAction, TEnum?> get, Action<TAction, TEnum?> set)
        where TAction : CutsceneActionData
        where TEnum : struct, Enum
        => new(key, CutsceneActionPropertyKind.Enum, true, typeof(TEnum), a => get((TAction)a), (a, v) => set((TAction)a, (TEnum?)v));

    private static CutsceneActionProperty Text<TAction>(string key, Func<TAction, string> get, Action<TAction, string> set)
        where TAction : CutsceneActionData
        => new(key, CutsceneActionPropertyKind.String, true, typeof(string), a => get((TAction)a), (a, v) => set((TAction)a, (string)v!));

    /// <summary>The list of the action is read-only and live: reading copies it, writing replaces its content from a copy.</summary>
    private static CutsceneActionProperty TextList<TAction>(string key, Func<TAction, List<string>> list)
        where TAction : CutsceneActionData
        => new(
            key,
            CutsceneActionPropertyKind.StringList,
            false,
            typeof(List<string>),
            a => new List<string>(list((TAction)a)),
            (a, v) =>
            {
                var copy = new List<string>((IEnumerable<string>)v!);
                var target = list((TAction)a);
                target.Clear();
                target.AddRange(copy);
            });
}
