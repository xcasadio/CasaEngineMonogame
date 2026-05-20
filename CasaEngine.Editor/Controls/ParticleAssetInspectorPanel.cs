using System;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Particles.Authoring;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class ParticleAssetInspectorPanel : IDisposable
{
    private readonly MGWindow _window;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _emitterStack;

    private ParticleEffectAsset? _particleAsset;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private bool _isDirty;

    public ParticleAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    public ParticleEffectAsset? LoadedParticleAsset => _particleAsset;

    public string? LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    public event Action<ParticleAssetInspectorPanel>? DirtyStateChanged;

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Particle Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No particle asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .particle asset from the Content Browser.")
        {
            Margin = new Thickness(8, 0, 8, 8),
            Opacity = 0.7f,
            WrapText = true,
        };

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
        };
        toolbar.TryAddChild(CreateButton("Save", SaveLoadedAsset));
        toolbar.TryAddChild(CreateButton("Reload", ReloadLoadedAsset));

        _emitterStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_emitterStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);
        _historyContextId = historyContextId;
    }

    public void LoadAsset(ParticleEffectAsset particleAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(particleAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _particleAsset = particleAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        SetDirty(false);

        if (TryGetHistoryContext(out var historyContext))
        {
            EditorDirtyStateService.Current.MarkSaved(historyContext);
        }

        RefreshInspector();
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return false;
        }

        if (IsDirty)
        {
            SetStatus($"Unsaved changes kept for {_loadedRelativePath}");
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadAsset(fullPath, out var particleAsset))
        {
            return false;
        }

        LoadAsset(particleAsset, fullPath);
        return true;
    }

    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;

        if (_particleAsset == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No particle asset is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            SetStatus($"Already saved {_loadedRelativePath}");
            return true;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _particleAsset, EditorAssetSaveSource.ParticleEffectEditorPanel);
            SetDirty(false);

            if (TryGetHistoryContext(out var historyContext))
            {
                EditorDirtyStateService.Current.MarkSaved(historyContext);
            }

            SetStatus($"Saved {_loadedRelativePath}");
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = exception.Message;
            SetStatus($"Failed to save {_loadedRelativePath}: {exception.Message}");
            return false;
        }
    }

    public void Dispose()
    {
    }

    public static bool TryLoadAsset(string fullPath, out ParticleEffectAsset particleAsset)
    {
        particleAsset = new ParticleEffectAsset();

        if (!File.Exists(fullPath)
            || !Path.GetExtension(fullPath).Equals(Constants.FileNameExtensions.Particle, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["emitters"] is not JArray)
            {
                return false;
            }

            particleAsset.Load(document);
            particleAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(particleAsset.FileName)
                            ?? AssetCatalog.GetByFileName(particleAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                particleAsset.Name = assetInfo.Name;
                particleAsset.AssetId = assetInfo.Id;
                particleAsset.FileName = assetInfo.FileName;
            }
            else
            {
                particleAsset.AssetId = particleAsset.Id;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveLoadedAsset()
    {
        if (!TrySaveLoadedAsset(out string? errorMessage) && !string.IsNullOrWhiteSpace(errorMessage))
        {
            SetStatus(errorMessage);
        }
    }

    private void ReloadLoadedAsset()
    {
        if (!ReloadFromDisk())
        {
            SetStatus("Unable to reload particle asset.");
        }
    }

    private void RefreshInspector()
    {
        if (_emitterStack == null || _headerText == null || _sourceText == null || _statusText == null)
        {
            return;
        }

        _emitterStack.TryRemoveAll();

        if (_particleAsset == null)
        {
            _headerText.Text = "[b]Particle Inspector[/b]";
            _sourceText.Text = "No particle asset loaded.";
            _statusText.Text = "Open a .particle asset from the Content Browser.";
            return;
        }

        _headerText.Text = $"[b]{EscapeMarkup(_particleAsset.Name)}[/b]";
        _sourceText.Text = BuildSourceText(_particleAsset);
        _statusText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? string.Empty
            : IsDirty ? $"Modified {EscapeMarkup(_loadedRelativePath)}" : $"Asset: {EscapeMarkup(_loadedRelativePath)}";

        _emitterStack.TryAddChild(BuildSectionHeader($"Emitters ({_particleAsset.Emitters.Count})"));
        if (_particleAsset.Emitters.Count == 0)
        {
            _emitterStack.TryAddChild(BuildText("No emitters in asset."));
            return;
        }

        for (int emitterIndex = 0; emitterIndex < _particleAsset.Emitters.Count; emitterIndex++)
        {
            _emitterStack.TryAddChild(BuildEmitterSummary(_particleAsset.Emitters[emitterIndex], emitterIndex));
        }
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 84,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private MGTextBlock BuildSectionHeader(string title)
        => new(_window, $"[b]{EscapeMarkup(title)}[/b]")
        {
            Margin = new Thickness(0, 6, 0, 2),
            WrapText = true,
        };

    private MGTextBlock BuildText(string text)
        => new(_window, EscapeMarkup(text))
        {
            Margin = new Thickness(0, 0, 0, 2),
            Opacity = 0.8f,
            WrapText = true,
        };

    private MGTextBlock BuildEmitterSummary(ParticleEmitterDefinition emitter, int emitterIndex)
    {
        string emitterName = string.IsNullOrWhiteSpace(emitter.Name) ? $"Emitter {emitterIndex + 1}" : emitter.Name;
        string summary = string.Create(CultureInfo.InvariantCulture,
            $"{emitterIndex + 1}. {emitterName}\nEnabled: {emitter.Enabled}  Looping: {emitter.Looping}  Duration: {emitter.Duration:0.###}s  Max: {emitter.MaxParticles}\nRate: {emitter.Emission.RateOverTime:0.###}/s  Shape: {emitter.Shape.ShapeType}  Blend: {emitter.Renderer.BlendMode}  Texture: {FormatGuid(emitter.Renderer.TextureAssetId)}");

        return new MGTextBlock(_window, EscapeMarkup(summary))
        {
            Margin = new Thickness(0, 0, 0, 6),
            WrapText = true,
        };
    }

    private string BuildSourceText(ParticleEffectAsset particleAsset)
    {
        string id = particleAsset.AssetId != Guid.Empty ? particleAsset.AssetId.ToString() : particleAsset.Id.ToString();
        string source = string.IsNullOrWhiteSpace(_loadedRelativePath) ? "<unsaved>" : _loadedRelativePath;
        return $"Source: {EscapeMarkup(source)}\nId: {id}  Version: {particleAsset.Version}";
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private void SetDirty(bool isDirty)
    {
        if (_isDirty == isDirty)
        {
            return;
        }

        _isDirty = isDirty;
        DirtyStateChanged?.Invoke(this);
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Particle, _historyContextId);
        return true;
    }

    private static string FormatGuid(Guid value)
        => value == Guid.Empty ? "<none>" : value.ToString();

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}