using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>One action type a user can create, with the category it is listed under.</summary>
public sealed record CutsceneActionKind(string Type, string Category)
{
    /// <summary>The label of the kind in a flat list, such as <c>Sprites / MoveSprite</c>.</summary>
    public string DisplayName => $"{Category} / {Type}";
}

/// <summary>
/// The action types the editor can create, how a new action is initialised (ADR-0097) and which actions hold children.
/// </summary>
public static class CutsceneActionCatalog
{
    public const string FlowCategory = "Flow";
    public const string TimingCategory = "Timing";
    public const string MovementCategory = "Movement";
    public const string SpritesCategory = "Sprites";
    public const string AudioCategory = "Audio";
    public const string ScreenCategory = "Screen";
    public const string WorldAndInputCategory = "World and input";

    private static readonly CutsceneActionKind[] AllKinds =
    [
        new(CutsceneActionTypes.Sequence, FlowCategory),
        new(CutsceneActionTypes.Parallel, FlowCategory),
        new(CutsceneActionTypes.Repeat, FlowCategory),
        new(CutsceneActionTypes.Wait, TimingCategory),
        new(CutsceneActionTypes.WaitFrames, TimingCategory),
        new(CutsceneActionTypes.MoveTo, MovementCategory),
        new(CutsceneActionTypes.NavigateTo, MovementCategory),
        new(CutsceneActionTypes.ShowSprite, SpritesCategory),
        new(CutsceneActionTypes.HideSprite, SpritesCategory),
        new(CutsceneActionTypes.MoveSprite, SpritesCategory),
        new(CutsceneActionTypes.TintSprite, SpritesCategory),
        new(CutsceneActionTypes.PlayVideoSprite, SpritesCategory),
        new(CutsceneActionTypes.StartVideoSprite, SpritesCategory),
        new(CutsceneActionTypes.PlaySound, AudioCategory),
        new(CutsceneActionTypes.PlayMusic, AudioCategory),
        new(CutsceneActionTypes.StopMusic, AudioCategory),
        new(CutsceneActionTypes.FadeMusic, AudioCategory),
        new(CutsceneActionTypes.FadeScreen, ScreenCategory),
        new(CutsceneActionTypes.FadeScreenFrames, ScreenCategory),
        new(CutsceneActionTypes.OpenScreen, WorldAndInputCategory),
        new(CutsceneActionTypes.PlayVideo, WorldAndInputCategory),
        new(CutsceneActionTypes.ChangeWorld, WorldAndInputCategory),
        new(CutsceneActionTypes.ExitOnInput, WorldAndInputCategory),
    ];

    /// <summary>The creatable types, grouped by category in the order of the categories.</summary>
    public static IReadOnlyList<CutsceneActionKind> Kinds => AllKinds;

    /// <summary>
    /// Creates an action of a type with the defaults of its class, except <c>Repeat.count</c> and
    /// <c>WaitFrames.frames</c>, which start at 1. The action can be invalid until its required fields are filled.
    /// </summary>
    /// <exception cref="ArgumentException">The type is not in the catalogue.</exception>
    public static CutsceneActionData Create(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type switch
        {
            CutsceneActionTypes.Wait => new WaitCutsceneActionData(),
            CutsceneActionTypes.MoveTo => new MoveToCutsceneActionData(),
            CutsceneActionTypes.NavigateTo => new NavigateToCutsceneActionData(),
            CutsceneActionTypes.Sequence => new SequenceCutsceneActionData(),
            CutsceneActionTypes.Parallel => new ParallelCutsceneActionData(),
            CutsceneActionTypes.PlaySound => new PlaySoundCutsceneActionData(),
            CutsceneActionTypes.PlayMusic => new PlayMusicCutsceneActionData(),
            CutsceneActionTypes.StopMusic => new StopMusicCutsceneActionData(),
            CutsceneActionTypes.FadeMusic => new FadeMusicCutsceneActionData(),
            CutsceneActionTypes.FadeScreen => new FadeScreenCutsceneActionData(),
            CutsceneActionTypes.WaitFrames => new WaitFramesCutsceneActionData { Frames = 1 },
            CutsceneActionTypes.FadeScreenFrames => new FadeScreenFramesCutsceneActionData(),
            CutsceneActionTypes.OpenScreen => new OpenScreenCutsceneActionData(),
            CutsceneActionTypes.PlayVideo => new PlayVideoCutsceneActionData(),
            CutsceneActionTypes.ChangeWorld => new ChangeWorldCutsceneActionData(),
            CutsceneActionTypes.ShowSprite => new ShowSpriteCutsceneActionData(),
            CutsceneActionTypes.HideSprite => new HideSpriteCutsceneActionData(),
            CutsceneActionTypes.PlayVideoSprite => new PlayVideoSpriteCutsceneActionData(),
            CutsceneActionTypes.StartVideoSprite => new StartVideoSpriteCutsceneActionData(),
            CutsceneActionTypes.MoveSprite => new MoveSpriteCutsceneActionData(),
            CutsceneActionTypes.TintSprite => new TintSpriteCutsceneActionData(),
            CutsceneActionTypes.Repeat => new RepeatCutsceneActionData { Count = 1 },
            CutsceneActionTypes.ExitOnInput => new ExitOnInputCutsceneActionData(),
            _ => throw new ArgumentException($"'{type}' is not an action type of the catalogue.", nameof(type)),
        };
    }

    /// <summary>True for Sequence, Parallel and Repeat, which hold a list of children.</summary>
    public static bool CanHaveChildren(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return CutsceneLoadFidelity.GetChildren(action) != null;
    }

    /// <summary>The children of a container, in order; an empty list for any other action.</summary>
    public static IReadOnlyList<CutsceneActionData> GetChildren(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return CutsceneLoadFidelity.GetChildren(action) ?? (IReadOnlyList<CutsceneActionData>)Array.Empty<CutsceneActionData>();
    }

    /// <summary>
    /// Copies an action and its whole subtree by saving it in a temporary asset and loading it back, so that the clone
    /// shares no list with the source. What a save does not write (format errors, unknown types) is not copied.
    /// </summary>
    public static CutsceneActionData Clone(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var temporary = new CutsceneAsset { RootAction = action };
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(temporary, node);

        var copy = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(copy, node);
        return copy.RootAction!;
    }
}
