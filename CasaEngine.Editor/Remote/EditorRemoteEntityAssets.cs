using System;
using System.IO;

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
}
