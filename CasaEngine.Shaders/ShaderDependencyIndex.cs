using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CasaEngine.Shaders;

public sealed class ShaderDependencyIndex
{
    private static readonly Regex IncludeRegex = new(
        "^\\s*#include\\s+[\"<](?<path>[^\">]+)[\">]",
        RegexOptions.Compiled);

    private readonly string _sourceRootDirectory;
    private readonly string[] _rootShaderRelativePaths;
    private readonly Dictionary<string, HashSet<string>> _affectedRootsByDependencyPath = new(StringComparer.OrdinalIgnoreCase);

    public ShaderDependencyIndex(string sourceRootDirectory, IEnumerable<string> rootShaderRelativePaths)
    {
        if (string.IsNullOrWhiteSpace(sourceRootDirectory))
        {
            throw new ArgumentException("A shader source root directory is required.", nameof(sourceRootDirectory));
        }

        ArgumentNullException.ThrowIfNull(rootShaderRelativePaths);

        _sourceRootDirectory = Path.GetFullPath(sourceRootDirectory);
        _rootShaderRelativePaths = NormalizeRootShaderRelativePaths(rootShaderRelativePaths);

        Rebuild();
    }

    public IReadOnlyList<string> RootShaderRelativePaths => _rootShaderRelativePaths;

    public IReadOnlyCollection<string> GetAffectedRootShaders(string changedRelativePath)
    {
        if (string.IsNullOrWhiteSpace(changedRelativePath))
        {
            return Array.Empty<string>();
        }

        string normalizedChangedPath = NormalizeRelativePath(changedRelativePath);
        if (!_affectedRootsByDependencyPath.TryGetValue(normalizedChangedPath, out var affectedRoots))
        {
            return Array.Empty<string>();
        }

        var result = new string[affectedRoots.Count];
        affectedRoots.CopyTo(result);
        return result;
    }

    public void Rebuild()
    {
        _affectedRootsByDependencyPath.Clear();

        for (int index = 0; index < _rootShaderRelativePaths.Length; index++)
        {
            string rootShaderRelativePath = _rootShaderRelativePaths[index];
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectDependencies(
                rootShaderRelativePath,
                dependencies,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            dependencies.Add(rootShaderRelativePath);
            RegisterAffectedRoots(rootShaderRelativePath, dependencies);
        }
    }

    private void CollectDependencies(
        string currentRelativePath,
        HashSet<string> destination,
        HashSet<string> visited)
    {
        string normalizedCurrentPath = NormalizeRelativePath(currentRelativePath);
        if (!visited.Add(normalizedCurrentPath))
        {
            return;
        }

        string fullPath = GetFullPath(normalizedCurrentPath);
        if (!File.Exists(fullPath))
        {
            return;
        }

        string currentDirectory = Path.GetDirectoryName(normalizedCurrentPath) ?? string.Empty;
        foreach (string includePath in ParseIncludePaths(fullPath))
        {
            string combinedIncludePath = string.IsNullOrEmpty(currentDirectory)
                ? includePath
                : Path.Combine(currentDirectory, includePath);
            string normalizedIncludePath = NormalizeRelativePath(combinedIncludePath);

            if (!destination.Add(normalizedIncludePath))
            {
                continue;
            }

            CollectDependencies(normalizedIncludePath, destination, visited);
        }
    }

    private void RegisterAffectedRoots(string rootShaderRelativePath, HashSet<string> dependencies)
    {
        foreach (string dependency in dependencies)
        {
            if (!_affectedRootsByDependencyPath.TryGetValue(dependency, out var affectedRoots))
            {
                affectedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _affectedRootsByDependencyPath.Add(dependency, affectedRoots);
            }

            affectedRoots.Add(rootShaderRelativePath);
        }
    }

    private string GetFullPath(string relativePath)
    {
        string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_sourceRootDirectory, normalizedRelativePath));
    }

    private static IEnumerable<string> ParseIncludePaths(string fullPath)
    {
        foreach (string line in File.ReadLines(fullPath))
        {
            Match match = IncludeRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string includePath = match.Groups["path"].Value.Trim();
            if (includePath.Length == 0)
            {
                continue;
            }

            yield return includePath;
        }
    }

    private static string[] NormalizeRootShaderRelativePaths(IEnumerable<string> rootShaderRelativePaths)
    {
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedPaths = new List<string>();

        foreach (string rootShaderRelativePath in rootShaderRelativePaths)
        {
            if (string.IsNullOrWhiteSpace(rootShaderRelativePath))
            {
                continue;
            }

            string normalizedPath = NormalizeRelativePath(rootShaderRelativePath);
            if (!uniquePaths.Add(normalizedPath))
            {
                continue;
            }

            normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths.ToArray();
    }

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}