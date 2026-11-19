using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;

namespace CasaEngine.Editor.Remote;

/// <summary>Rules the remote commands apply to an entity asset panel (ADR-0129).</summary>
internal static class EditorRemoteEntityAssets
{
    /// <summary>
    /// Plan P13, security review S3-1: a panel is found by an asset id, and another file (an uncatalogued copy) can
    /// carry that id, so a command acts on a panel only when it holds the resolved file. True exactly when
    /// <paramref name="loadedRelativePath"/> is a project-relative path without <c>..</c> that names
    /// <paramref name="expectedFullPath"/>, ignoring case. Pure: no file is touched.
    /// </summary>
    public static bool IsLoadedFile(string projectDirectory, string loadedRelativePath, string expectedFullPath)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory)
            || string.IsNullOrWhiteSpace(loadedRelativePath)
            || string.IsNullOrWhiteSpace(expectedFullPath))
        {
            return false;
        }

        if (Path.IsPathRooted(loadedRelativePath))
        {
            return false;
        }

        foreach (string segment in loadedRelativePath.Split('/', '\\'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        try
        {
            string loadedFullPath = Path.GetFullPath(Path.Combine(Path.GetFullPath(projectDirectory), loadedRelativePath));
            return string.Equals(loadedFullPath, Path.GetFullPath(expectedFullPath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A path the system cannot normalise names no file the panel could hold: refused, never thrown at a command.
            return false;
        }
    }

    /// <summary>
    /// Plan P6, security review S3-4: creates an empty entity asset (an entity named <see cref="RemoteNewAssetFile.Name"/>,
    /// without component) at a prepared new file. The file is reserved first (<see cref="EditorRemoteAssetCreation.Reserve"/>:
    /// a file that appeared since <c>Prepare</c> throws the remote refusal, nothing is undone), then written by
    /// <paramref name="write"/>, then the catalog entry is added and the catalog saved by <paramref name="saveCatalog"/>.
    /// The entry comes after the write, so a failed write leaves no entry. On any exception after the reservation, the
    /// entry is removed from memory once its addition was reached and the reserved file is deleted, each step guarded on
    /// its own; the failure is logged and returned through <paramref name="error"/>.
    /// Residuals: an editor stop between two steps can leave the reserved file alone; when the catalog save fails after
    /// the entry was added, the content browser may show the entry until its next refresh, and a partly written
    /// <c>AssetInfos.json</c> may keep it.
    /// </summary>
    public static bool Create(RemoteNewAssetFile file, Action<string, Entity> write, Action saveCatalog, out Guid assetId, out string error)
    {
        assetId = Guid.Empty;
        EditorRemoteAssetCreation.Reserve(file.FullPath);
        var entity = new Entity
        {
            Name = file.Name,
            FileName = file.RelativeFile,
        };

        // Set before the call: Add can throw from a subscriber after the entry was inserted.
        bool addReached = false;
        try
        {
            write(file.RelativeFile, entity);
            addReached = true;
            EditorAssetCatalogService.Add(entity);
            saveCatalog();
        }
        catch (Exception ex)
        {
            Logs.WriteWarning($"[Remote] The entity asset '{file.RelativeFile}' could not be created, the creation is undone: {ex.GetType().Name}: {ex.Message}");
            if (addReached)
            {
                try
                {
                    // The id is fresh: this removes the entry of this creation only, in memory.
                    EditorAssetCatalogService.TryRemoveEntry(entity.Id, out _);
                }
                catch (Exception removeEx)
                {
                    Logs.WriteWarning($"[Remote] The catalog entry of '{file.RelativeFile}' could not be removed: {removeEx.Message}");
                }
            }

            try
            {
                File.Delete(file.FullPath);
            }
            catch (Exception deleteEx)
            {
                Logs.WriteWarning($"[Remote] The reserved file '{file.RelativeFile}' could not be deleted: {deleteEx.Message}");
            }

            error = $"'{file.RelativeFile}' could not be created: {ex.Message}";
            return false;
        }

        assetId = entity.Id;
        error = null;
        return true;
    }
}
