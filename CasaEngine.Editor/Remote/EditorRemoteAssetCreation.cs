using System;
using System.IO;
using System.Text;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Remote;

/// <summary>Where a new asset goes: its project-relative file and its full path.</summary>
internal readonly record struct RemoteNewAssetFile(string RelativeFile, string FullPath, string Name);

/// <summary>
/// Checks and writes a new asset file for <c>material.create</c>, <c>particle.create</c> and <c>sound.create</c>
/// (ADR-0093 P4, security review S2-4): a name (the world name rule), an existing folder inside the project, the
/// type's extension, no name already in the catalog (any type, any case), no catalog entry for the file, no existing
/// file; the file is created with <see cref="FileMode.CreateNew"/>, never over another one.
/// </summary>
internal static class EditorRemoteAssetCreation
{
    /// <summary>Validates the destination; throws a remote refusal when it is not allowed.</summary>
    public static RemoteNewAssetFile Prepare(string projectDirectory, string folder, string name, string type)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, "No project is open.");
        }

        if (!EditorRemoteValidation.IsValidWorldName(name))
        {
            throw new EditorRemoteArgumentException($"'{name}' is not a valid asset name: letters, digits, space, '_' or '-', 64 characters at most.");
        }

        string folderPath = string.IsNullOrWhiteSpace(folder) || folder.Trim() is "." or "/" ? string.Empty : folder.Replace('\\', '/').Trim('/');
        string folderFull = Path.GetFullPath(projectDirectory);
        if (folderPath.Length > 0 && !EditorRemoteValidation.TryResolveInsideProject(projectDirectory, folderPath, out folderFull))
        {
            throw new EditorRemoteArgumentException($"'{folder}' is not a folder inside the project.");
        }

        if (!Directory.Exists(folderFull))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"The folder '{folder}' does not exist.");
        }

        if ((File.GetAttributes(folderFull) & FileAttributes.ReparsePoint) != 0)
        {
            throw new EditorRemoteArgumentException($"'{folder}' is a link: assets are created in real project folders only.");
        }

        string relativeFile = (folderPath.Length > 0 ? folderPath + "/" : string.Empty) + name + EditorRemoteAssetTypes.Extension(type);
        if (!EditorRemoteValidation.TryResolveInsideProject(projectDirectory, relativeFile, out string fullPath))
        {
            throw new EditorRemoteArgumentException($"'{relativeFile}' is not inside the project.");
        }

        foreach (AssetInfo info in AssetCatalog.AssetInfos)
        {
            if (string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                throw new EditorRemoteRefusedException(RemoteErrorReasons.InvalidArgument, $"The asset catalog already has an asset named '{info.Name}'.");
            }
        }

        if (AssetCatalog.GetByFileName(relativeFile) != null || AssetCatalog.GetByFileName(relativeFile.Replace('/', '\\')) != null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.InvalidArgument, $"The asset catalog already has an entry for '{relativeFile}'.");
        }

        if (File.Exists(fullPath))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.InvalidArgument, $"'{relativeFile}' already exists.");
        }

        return new RemoteNewAssetFile(relativeFile.Replace('/', Path.DirectorySeparatorChar), fullPath, name);
    }

    /// <summary>
    /// Creates the new file empty, failing if it appeared in the meantime, so that a type's usual writer (which
    /// overwrites) then fills a file this command owns.
    /// </summary>
    public static void Reserve(string fullPath) => WriteNew(fullPath, string.Empty);

    /// <summary>Writes the new file, failing if it appeared in the meantime.</summary>
    public static void WriteNew(string fullPath, string content)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (IOException ex) when (File.Exists(fullPath))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.InvalidArgument, $"'{fullPath}' already exists: {ex.Message}");
        }
    }
}
