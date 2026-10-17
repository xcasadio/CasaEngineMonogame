using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The game modes whose default pawn was edited from the world inspector since the last project save (ADR-0077).
/// A game mode is shared by every world that uses it, so the edit lands on the shared <see cref="PlayerStartupSettings"/>
/// instance and its <c>.gameMode</c> file is written only with the project save. Each entry holds a counted handle on the
/// instance (ADR-0037), so a world reloaded meanwhile finds the same modified instance, and remembers the pawn the mode had
/// when it was first recorded, so an abandon puts it back and a save skips a mode whose pawn went back to it.
/// </summary>
public sealed class PendingGameModeEdits
{
    private sealed class Entry
    {
        public Entry(AssetHandle<PlayerStartupSettings> handle, Guid originalPawn)
        {
            Handle = handle;
            OriginalPawn = originalPawn;
        }

        public AssetHandle<PlayerStartupSettings> Handle { get; }

        public Guid OriginalPawn { get; }
    }

    private readonly Dictionary<Guid, Entry> _entries = new();

    /// <summary>The number of recorded game modes.</summary>
    public int Count => _entries.Count;

    public bool IsPending(Guid gameModeAssetId) => _entries.ContainsKey(gameModeAssetId);

    /// <summary>
    /// Records the mode before its pawn changes (by a command or by its undo): the first call acquires the handle and keeps the
    /// current pawn as the original one; later calls do nothing.
    /// </summary>
    public void Record(AssetContentManager assetContentManager, Guid gameModeAssetId)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (_entries.ContainsKey(gameModeAssetId))
        {
            return;
        }

        var handle = assetContentManager.Acquire<PlayerStartupSettings>(gameModeAssetId);
        _entries[gameModeAssetId] = new Entry(handle, handle.Asset.DefaultPawnAssetId);
    }

    /// <summary>
    /// Writes every mode whose pawn differs from its original one, then releases the written and the unchanged entries.
    /// A failed write is logged and keeps its entry (handle and original pawn); the method then returns false.
    /// </summary>
    public bool TrySaveAll(out string errorMessage)
    {
        var errors = new List<string>();

        foreach (var (modeId, entry) in _entries.ToArray())
        {
            var settings = entry.Handle.Asset;
            if (settings.DefaultPawnAssetId != entry.OriginalPawn)
            {
                var fileName = AssetCatalog.Get(modeId)?.FileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    AddError(errors, modeId, "the game mode is not in the asset catalog");
                    continue;
                }

                try
                {
                    EditorAssetWriterService.SaveAsset(fileName, settings, EditorAssetSaveSource.WorldInspector);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    AddError(errors, modeId, exception.Message);
                    continue;
                }
            }

            entry.Handle.Dispose();
            _entries.Remove(modeId);
        }

        errorMessage = string.Join(" ", errors);
        return errors.Count == 0;
    }

    /// <summary>Puts every recorded instance's original pawn back, releases every handle and forgets the modes.</summary>
    public void DiscardAll()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Handle.Asset.DefaultPawnAssetId = entry.OriginalPawn;
            entry.Handle.Dispose();
        }

        _entries.Clear();
    }

    private static void AddError(List<string> errors, Guid modeId, string reason)
    {
        var name = AssetCatalog.Get(modeId)?.Name;
        var label = string.IsNullOrEmpty(name) ? modeId.ToString() : $"{name} ({modeId})";
        var message = $"Cannot save game mode {label}: {reason}.";
        Logs.WriteWarning(message);
        errors.Add(message);
    }
}
