using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Log;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Shaders;

namespace CasaEngine.Editor.Runtime;

internal sealed class EditorShaderSourceHotReloadService : IDisposable
{
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly object _syncRoot = new();
    private readonly HashSet<string> _pendingChangedRelativePaths = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher _watcher;
    private ShaderDependencyIndex _dependencyIndex;
    private string _contentSourceRoot;
    private TargetPlatform _targetPlatform = TargetPlatform.Windows;

    public EditorShaderSourceHotReloadService(HostedEditorGameAdapter editorRuntime)
    {
        _editorRuntime = editorRuntime ?? throw new ArgumentNullException(nameof(editorRuntime));
    }

    public void Reconfigure()
    {
        DisposeWatcher();
        ClearPendingChanges();

        if (!TryResolveContentSourceRoot(out string contentSourceRoot))
        {
            _dependencyIndex = null;
            _contentSourceRoot = null;
            EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                "[Editor] Shader source hot reload disabled: could not locate CasaEngine/Content sources.");
            return;
        }

        string shaderSourceDirectory = Path.Combine(contentSourceRoot, "Shaders");
        if (!Directory.Exists(shaderSourceDirectory))
        {
            _dependencyIndex = null;
            _contentSourceRoot = null;
            EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                $"[Editor] Shader source hot reload disabled: '{shaderSourceDirectory}' does not exist.");
            return;
        }

        _contentSourceRoot = contentSourceRoot;
        _targetPlatform = ResolveTargetPlatform(contentSourceRoot);
        _dependencyIndex = new ShaderDependencyIndex(contentSourceRoot, GetRuntimeShaderSourcePaths());
        _watcher = new FileSystemWatcher(shaderSourceDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            Filter = "*.*",
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnWatcherChanged;
        _watcher.Created += OnWatcherChanged;
        _watcher.Deleted += OnWatcherChanged;
        _watcher.Renamed += OnWatcherRenamed;
        _watcher.Error += OnWatcherError;

        EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
            $"[Editor] Shader source hot reload watching='{shaderSourceDirectory}' targetPlatform={_targetPlatform} rootShaders={BuiltInShaderCatalog.RuntimeShaders.Count}");
    }

    public void ProcessPendingChanges()
    {
        if (_dependencyIndex is null)
        {
            return;
        }

        string[] changedRelativePaths = DrainPendingChangedRelativePaths();
        if (changedRelativePaths.Length == 0)
        {
            return;
        }

        var affectedRootShaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < changedRelativePaths.Length; index++)
        {
            IReadOnlyCollection<string> affectedRoots = _dependencyIndex.GetAffectedRootShaders(changedRelativePaths[index]);
            foreach (string affectedRoot in affectedRoots)
            {
                affectedRootShaders.Add(affectedRoot);
            }
        }

        _dependencyIndex.Rebuild();

        foreach (string affectedRootShader in affectedRootShaders)
        {
            ReloadRootShader(affectedRootShader);
        }
    }

    public void Dispose()
    {
        DisposeWatcher();
    }

    private void ReloadRootShader(string rootShaderRelativePath)
    {
        if (_contentSourceRoot is null
            || !BuiltInShaderCatalog.TryGetBySourceRelativePath(rootShaderRelativePath, out var descriptor))
        {
            return;
        }

        string fullPath = Path.Combine(
            _contentSourceRoot,
            rootShaderRelativePath.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            ShaderCompiled compiledShader = ShaderCompiler.Compile(
                fullPath,
                string.Empty,
                _targetPlatform,
                EffectProcessorDebugMode.Optimize);

            if (!string.IsNullOrWhiteSpace(compiledShader.Logs))
            {
                EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                    $"[Editor] Shader compiler diagnostics for '{descriptor.SourceRelativePath}': {compiledShader.Logs}");
            }

            if (compiledShader.ByteCode == null || compiledShader.ByteCode.Length == 0)
            {
                EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                    $"[Editor] Shader hot reload skipped '{descriptor.SourceRelativePath}': compiler returned no bytecode.");
                return;
            }

            ShaderHotReloadMetrics hotReloadMetrics = _editorRuntime.ReloadBuiltInShader(descriptor.ContentName, compiledShader.ByteCode);
            if (hotReloadMetrics.ReloadedConsumerCount > 0)
            {
                EditorDiagnosticsBuffer.Append(LogVerbosity.Info,
                    $"[Editor] Hot reloaded shader source='{descriptor.SourceRelativePath}' content='{descriptor.ContentName}' reloadedConsumers={hotReloadMetrics.ReloadedConsumerCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");
                return;
            }

            EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                $"[Editor] Shader source reload compiled '{descriptor.SourceRelativePath}' but no runtime consumer accepted content='{descriptor.ContentName}'.");
        }
        catch (Exception ex)
        {
            EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
                $"[Editor] Shader hot reload failed for '{rootShaderRelativePath}': {ex.Message}");
        }
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        => QueueChangedFile(e.FullPath);

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        QueueChangedFile(e.OldFullPath);
        QueueChangedFile(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        EditorDiagnosticsBuffer.Append(LogVerbosity.Warning,
            $"[Editor] Shader source watcher encountered an error: {e.GetException()?.Message ?? "unknown error"}");
    }

    private void QueueChangedFile(string fullPath)
    {
        if (_contentSourceRoot is null
            || string.IsNullOrWhiteSpace(fullPath)
            || !IsWatchedShaderFile(fullPath))
        {
            return;
        }

        string normalizedFullPath = Path.GetFullPath(fullPath);
        string relativePath = Path.GetRelativePath(_contentSourceRoot, normalizedFullPath);
        if (relativePath.StartsWith("..", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string normalizedRelativePath = BuiltInShaderCatalog.NormalizeSourceRelativePath(relativePath);
        lock (_syncRoot)
        {
            _pendingChangedRelativePaths.Add(normalizedRelativePath);
        }
    }

    private string[] DrainPendingChangedRelativePaths()
    {
        lock (_syncRoot)
        {
            if (_pendingChangedRelativePaths.Count == 0)
            {
                return Array.Empty<string>();
            }

            var changedPaths = new string[_pendingChangedRelativePaths.Count];
            _pendingChangedRelativePaths.CopyTo(changedPaths);
            _pendingChangedRelativePaths.Clear();
            return changedPaths;
        }
    }

    private void ClearPendingChanges()
    {
        lock (_syncRoot)
        {
            _pendingChangedRelativePaths.Clear();
        }
    }

    private void DisposeWatcher()
    {
        if (_watcher == null)
        {
            return;
        }

        _watcher.Changed -= OnWatcherChanged;
        _watcher.Created -= OnWatcherChanged;
        _watcher.Deleted -= OnWatcherChanged;
        _watcher.Renamed -= OnWatcherRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
        _watcher = null;
    }

    private static bool IsWatchedShaderFile(string fullPath)
    {
        string extension = Path.GetExtension(fullPath);
        return string.Equals(extension, ".fx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".fxh", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetRuntimeShaderSourcePaths()
    {
        var sourcePaths = new string[BuiltInShaderCatalog.RuntimeShaders.Count];
        for (int index = 0; index < BuiltInShaderCatalog.RuntimeShaders.Count; index++)
        {
            sourcePaths[index] = BuiltInShaderCatalog.RuntimeShaders[index].SourceRelativePath;
        }

        return sourcePaths;
    }

    private static bool TryResolveContentSourceRoot(out string contentSourceRoot)
    {
        string currentDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        while (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            string candidate = Path.Combine(currentDirectory, "CasaEngine", "Content");
            if (Directory.Exists(candidate))
            {
                contentSourceRoot = candidate;
                return true;
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        contentSourceRoot = string.Empty;
        return false;
    }

    private static TargetPlatform ResolveTargetPlatform(string contentSourceRoot)
    {
        string mgcbPath = Path.Combine(contentSourceRoot, "Content.mgcb");
        if (!File.Exists(mgcbPath))
        {
            return TargetPlatform.Windows;
        }

        foreach (string line in File.ReadLines(mgcbPath))
        {
            string trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith("/platform:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string platformName = trimmedLine.Substring("/platform:".Length).Trim();
            if (Enum.TryParse(platformName, true, out TargetPlatform targetPlatform))
            {
                return targetPlatform;
            }
        }

        return TargetPlatform.Windows;
    }
}