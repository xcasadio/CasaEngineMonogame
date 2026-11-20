using System.Diagnostics;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// Junctions for the path segment cases (plan remote-path-segments P5, set A4; security findings S5-2 and S5-4). A query by
/// the plain path of a name ending with a space reaches the folder without the space, so such a junction is made under a
/// plain name, renamed through '\\?\' paths, checked from an enumeration of its parent and removed through its '\\?\' path
/// only, never through its target. Each case writes <see cref="RanMarker"/> once its junction is in place, or a
/// 'junction-case: inconclusive (...)' line when it cannot be made.
/// </summary>
internal static class PathSegmentJunctions
{
    public const string RanMarker = "junction-case: ran";

    public static string Extended(string fullPath) => @"\\?\" + fullPath;

    /// <summary>
    /// Makes <c>&lt;parent&gt;\&lt;name&gt;</c> a junction to <paramref name="target"/>. Every link made on the way is added to
    /// <paramref name="links"/> under its '\\?\' form, for <see cref="RemoveLinks"/>. False, with the inconclusive line
    /// written, when the junction cannot be made.
    /// </summary>
    public static bool TryCreate(string parent, string name, string target, ITestOutputHelper log, List<string> links)
    {
        string junction = Extended(Path.Combine(parent, name));
        string plain = Path.Combine(parent, "junction-" + Guid.NewGuid().ToString("N")[..8]);
        if (!TryRunMklink(plain, target, out string output))
        {
            log.WriteLine($"junction-case: inconclusive (mklink /J failed: {output.Trim()})");
            return false;
        }

        links.Add(Extended(plain));
        try
        {
            Directory.Move(Extended(plain), junction);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.WriteLine($"junction-case: inconclusive (the junction could not be renamed to '{name}': {exception.GetType().Name}: {exception.Message})");
            return false;
        }

        links.Remove(Extended(plain));
        links.Add(junction);
        if (!IsLinkInEnumeration(parent, name) || new DirectoryInfo(junction).LinkTarget == null)
        {
            log.WriteLine($"junction-case: inconclusive (no link named '{name}' in the enumeration of '{parent}')");
            return false;
        }

        log.WriteLine($"junction: '{junction}' -> '{target}'");
        log.WriteLine(RanMarker);
        return true;
    }

    /// <summary>A plain junction (no trailing space), registered under its '\\?\' form; false when mklink fails.</summary>
    public static bool TryCreatePlain(string link, string target, List<string> links, out string output)
    {
        if (!TryRunMklink(link, target, out output))
        {
            return false;
        }

        links.Add(Extended(link));
        return (File.GetAttributes(Extended(link)) & FileAttributes.ReparsePoint) != 0;
    }

    /// <summary>True when the attributes the enumeration of <paramref name="parent"/> returns for <paramref name="name"/> mark a reparse point.</summary>
    public static bool IsLinkInEnumeration(string parent, string name)
    {
        foreach (FileSystemInfo entry in new DirectoryInfo(Extended(parent)).EnumerateFileSystemInfos())
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                return (entry.Attributes & FileAttributes.ReparsePoint) != 0;
            }
        }

        return false;
    }

    /// <summary>True when the enumeration of <paramref name="parent"/> returns <paramref name="name"/> as a folder that is not a link.</summary>
    public static bool IsRealFolderInEnumeration(string parent, string name)
    {
        foreach (FileSystemInfo entry in new DirectoryInfo(Extended(parent)).EnumerateFileSystemInfos())
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                return (entry.Attributes & FileAttributes.Directory) != 0 && (entry.Attributes & FileAttributes.ReparsePoint) == 0;
            }
        }

        return false;
    }

    /// <summary>Removes each registered link alone (a '\\?\' path, not recursive), last made first.</summary>
    public static void RemoveLinks(List<string> links)
    {
        for (int i = links.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(links[i]))
            {
                Directory.Delete(links[i], recursive: false);
            }
        }

        links.Clear();
    }

    /// <summary>
    /// Deletes <paramref name="directory"/> recursively only when an enumeration of the whole tree (through '\\?\' paths,
    /// never into a link) finds no link left; otherwise throws and deletes nothing.
    /// </summary>
    public static void DeleteTreeWithoutLinks(string directory)
    {
        string link = FindLink(Extended(Path.GetFullPath(directory)));
        if (link != null)
        {
            throw new InvalidOperationException($"'{link}' is a link left under '{directory}': the folder is not deleted recursively.");
        }

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>The files of <paramref name="folder"/> (which holds no link) with a hash of their content, for an unchanged check.</summary>
    public static string[] Snapshot(string folder)
        => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

    private static string FindLink(string extendedDirectory)
    {
        foreach (FileSystemInfo entry in new DirectoryInfo(extendedDirectory).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return entry.FullName;
            }

            if ((entry.Attributes & FileAttributes.Directory) != 0 && FindLink(entry.FullName) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool TryRunMklink(string link, string target, out string output)
    {
        var start = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using Process process = Process.Start(start)!;
        output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
