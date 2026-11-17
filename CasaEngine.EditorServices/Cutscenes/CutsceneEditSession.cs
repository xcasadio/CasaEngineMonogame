using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// A cutscene being edited: the asset, whose action tree stays the only source of truth, and the load losses found when
/// it was opened (ADR-0095).
/// </summary>
public sealed class CutsceneEditSession
{
    private readonly List<CutsceneLoadLoss> _loadLosses = [];

    public CutsceneEditSession(CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Asset = asset;
    }

    public CutsceneAsset Asset { get; }

    /// <summary>What the loader dropped or rewrote silently and that no edit has replaced yet.</summary>
    public IReadOnlyList<CutsceneLoadLoss> LoadLosses => _loadLosses;

    internal void AddLoadLoss(CutsceneLoadLoss loss)
    {
        ArgumentNullException.ThrowIfNull(loss);
        _loadLosses.Add(loss);
    }

    /// <summary>The loss of a field of an action, or null.</summary>
    internal CutsceneLoadLoss? FindLoadLoss(CutsceneActionData action, string key)
    {
        for (int index = 0; index < _loadLosses.Count; index++)
        {
            if (ReferenceEquals(_loadLosses[index].Action, action) && _loadLosses[index].Key == key)
            {
                return _loadLosses[index];
            }
        }

        return null;
    }

    internal void RemoveLoadLoss(CutsceneLoadLoss loss) => _loadLosses.Remove(loss);
}
