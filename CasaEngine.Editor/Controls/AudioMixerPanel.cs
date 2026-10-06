#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Audio;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Newtonsoft.Json.Linq;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The document panel of one <c>.audioMixer</c> asset (plan T10.5, decisions P49 and P50): the editing shell around an
/// <see cref="AudioMixerDocument"/> (header, banners, Save, Reload, Apply, problem list). The body below the header is where the
/// bus strips and the detail views are built.
/// </summary>
/// <remarks>
/// <para>
/// The panel owns the document. Its edits go to the editor history of the context set by <see cref="SetHistoryContextId"/>
/// (one entry per user intent, see <see cref="AudioMixerDocument"/>); the dirty state follows the document. The controls are
/// refreshed when the document raises <see cref="AudioMixerDocument.Changed"/>, never per frame.
/// </para>
/// <para>
/// A document is live (it drives the project's mixer) only when it was given the applier of the project mixer; otherwise its
/// changes are saved but not heard, and a banner says so. <see cref="UpdateLiveBinding"/> and <see cref="DetachLive"/> follow a
/// project change; they go through the panel, which keeps the applier a reload hands to the new document. Reloading builds a
/// new document: the history of the context is cleared, since its entries belong to the previous document.
/// <see cref="Dispose"/> closes the document: it gives the live mixer back to the saved asset.
/// </para>
/// </remarks>
public sealed class AudioMixerPanel : IDisposable
{
    private const string LiveBannerText = "Live: changes are applied to the mixer";
    private const string SoftwareOnlyBannerText = "Effects, sends and ducking: software backend only";

    private readonly MGWindow _window;
    private readonly Func<AudioService?> _audioServiceProvider;

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGTextBlock? _liveBanner;
    private MGTextBlock? _notHeardBanner;
    private MGTextBlock? _softwareOnlyBanner;
    private MGButton? _saveButton;
    private MGButton? _reloadButton;
    private MGButton? _applyButton;
    private MGStackPanel? _problemsHost;
    private MGStackPanel? _bodyHost;

    private AudioMixerDocument? _document;
    private AudioMixerAssetApplier? _liveApplier;
    private string? _loadedRelativePath;
    private string? _loadedFullPath;
    private string? _historyContextId;
    private IReadOnlyList<string> _shownProblems = Array.Empty<string>();
    private bool _lastDirty;
    private bool _isDisposed;

    /// <param name="window">The window the panel's controls belong to.</param>
    /// <param name="audioServiceProvider">Gives the audio service of the editor, or null while there is none.</param>
    public AudioMixerPanel(MGWindow window, Func<AudioService?> audioServiceProvider)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _audioServiceProvider = audioServiceProvider ?? throw new ArgumentNullException(nameof(audioServiceProvider));
    }

    /// <summary>The document being edited, null until <see cref="LoadAsset"/>.</summary>
    public AudioMixerDocument? Document => _document;

    public string? LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _document?.IsDirty ?? false;

    /// <summary>Raised when <see cref="IsDirty"/> changed.</summary>
    public event Action<AudioMixerPanel>? DirtyStateChanged;

    /// <summary>
    /// Reads an <c>.audioMixer</c> file for the editor. False, with a message in <paramref name="error"/>, when the file is missing,
    /// unreadable or written by a newer version. <paramref name="asset"/> is never null; its <see cref="ObjectBase.AssetId"/> is
    /// the catalog id of the file, empty when the file is not in the catalog.
    /// </summary>
    public static bool TryLoadAsset(string fullPath, out AudioMixerAsset asset, out string error)
    {
        asset = new AudioMixerAsset();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            error = $"Cannot open audio mixer '{fullPath}': the file does not exist.";
            return false;
        }

        if (!Path.GetExtension(fullPath).Equals(Constants.FileNameExtensions.AudioMixer, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Cannot open '{fullPath}': it is not a {Constants.FileNameExtensions.AudioMixer} file.";
            return false;
        }

        try
        {
            asset.Load(JObject.Parse(File.ReadAllText(fullPath)));
        }
        catch (Exception exception)
        {
            asset = new AudioMixerAsset();
            error = $"Cannot open audio mixer '{Path.GetFileName(fullPath)}': {exception.Message}";
            return false;
        }

        asset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

        var assetInfo = AssetCatalog.GetByFileName(asset.FileName)
                        ?? AssetCatalog.GetByFileName(asset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (assetInfo != null)
        {
            asset.AssetId = assetInfo.Id;
        }

        return true;
    }

    /// <summary>Sets the history context the edits of this panel belong to (the id of its dock panel). Call it before <see cref="LoadAsset"/>.</summary>
    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);
        _historyContextId = historyContextId;
    }

    /// <summary>
    /// Starts editing <paramref name="asset"/>, read from <paramref name="fullPath"/> (see <see cref="TryLoadAsset"/>). The document is
    /// live when <paramref name="liveApplier"/> is the applier of the project mixer, and not live when it is null. Whatever was edited
    /// before is replaced; the history of the context starts again and the document is clean.
    /// </summary>
    public void LoadAsset(AudioMixerAsset asset, string fullPath, AudioMixerAssetApplier? liveApplier)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        CloseDocument();

        _loadedFullPath = fullPath;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        _liveApplier = liveApplier;

        if (TryGetHistoryContext(out var historyContext))
        {
            // The entries of the previous document would undo into a document nobody shows.
            EditorHistoryService.Current.Clear(historyContext);
            EditorDirtyStateService.Current.MarkSaved(historyContext);
        }

        _document = new AudioMixerDocument(asset, _loadedRelativePath, liveApplier, ExecuteCommand);
        _document.Changed += OnDocumentChanged;

        bool wasDirty = _lastDirty;
        _lastDirty = _document.IsDirty;
        if (wasDirty != _lastDirty)
        {
            DirtyStateChanged?.Invoke(this);
        }

        Refresh();
    }

    /// <summary>
    /// Reads the file again and replaces the document; a live document applies the new asset to the live mixer. Refused while the
    /// document has unsaved changes.
    /// </summary>
    public bool ReloadFromDisk()
    {
        if (_document == null || string.IsNullOrWhiteSpace(_loadedFullPath))
        {
            return false;
        }

        // A fader gesture still open is a change that is not saved yet.
        _document.EndGesture();
        if (_document.IsDirty)
        {
            SetStatus("Unsaved changes kept: save or undo them before reloading.");
            return false;
        }

        if (!TryLoadAsset(_loadedFullPath, out var asset, out string error))
        {
            SetStatus(error);
            return false;
        }

        bool wasLive = _document.IsLive;
        LoadAsset(asset, _loadedFullPath, wasLive ? _liveApplier : null);
        if (wasLive)
        {
            _document!.ApplyToLive();
        }

        SetStatus($"Reloaded {_loadedRelativePath}");
        return true;
    }

    /// <summary>Writes the asset to its file, then marks the history context saved. False, with a message, when it cannot be written.</summary>
    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;

        if (_document == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No audio mixer is loaded.";
            return false;
        }

        if (!_document.TrySave(out string error))
        {
            errorMessage = error;
            SetStatus($"Cannot save {_loadedRelativePath}: {error}");
            return false;
        }

        if (TryGetHistoryContext(out var historyContext))
        {
            EditorDirtyStateService.Current.MarkSaved(historyContext);
        }

        SetStatus($"Saved {_loadedRelativePath}");
        return true;
    }

    /// <summary>Applies the current asset to the live mixer. Nothing happens when the document is not live.</summary>
    public void ApplyToLive()
    {
        if (_document == null)
        {
            return;
        }

        if (!_document.IsLive)
        {
            SetStatus("This asset is not the project mixer: nothing to apply.");
            return;
        }

        _document.ApplyToLive();
        SetStatus("Applied to the live mixer");
    }

    /// <summary>Stops driving the live mixer (the project was closed). The edits stay in the document.</summary>
    public void DetachLive()
    {
        _document?.DetachLive();
    }

    /// <summary>
    /// Follows the project's mixer after a project was applied: the document goes live when it is the asset of the project
    /// (<paramref name="projectAssetId"/>), and is not live otherwise.
    /// </summary>
    public void UpdateLiveBinding(Guid projectAssetId, AudioMixerAssetApplier? applier)
    {
        if (_document == null)
        {
            return;
        }

        _document.UpdateLiveBinding(projectAssetId, applier!);
        if (_document.IsLive)
        {
            _liveApplier = applier;
        }
    }

    /// <summary>Builds the panel once, and returns the same root afterwards. Use it as a dock panel content factory.</summary>
    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Audio Mixer[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No audio mixer loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _liveBanner = CreateBanner(LiveBannerText);
        _notHeardBanner = CreateBanner(string.Empty);
        _softwareOnlyBanner = CreateBanner(SoftwareOnlyBannerText);

        _statusText = new MGTextBlock(_window, "Open a .audioMixer asset from the Content Browser.")
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
        _saveButton = CreateButton("Save", SaveLoadedAsset, 84);
        _reloadButton = CreateButton("Reload", () => ReloadFromDisk(), 84);
        _applyButton = CreateButton("Apply to live mixer", ApplyToLive, 150);
        toolbar.TryAddChild(_saveButton);
        toolbar.TryAddChild(_reloadButton);
        toolbar.TryAddChild(_applyButton);

        _problemsHost = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(8, 0, 8, 8),
        };

        _bodyHost = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
        };

        var scrolled = new MGStackPanel(_window, Orientation.Vertical);
        scrolled.TryAddChild(_problemsHost);
        scrolled.TryAddChild(_bodyHost);

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(scrolled);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_liveBanner, Dock.Top);
        _root.TryAddChild(_notHeardBanner, Dock.Top);
        _root.TryAddChild(_softwareOnlyBanner, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        Refresh();
        return _root;
    }

    /// <summary>Reads what the panel shows from the audio service. Call it on every editor frame while the panel is the active document.</summary>
    /// <param name="elapsedSeconds">The time since the previous call.</param>
    public void Update(float elapsedSeconds)
    {
        // The shell shows no level yet: the bus strips read their meters here.
    }

    /// <summary>Forgets the levels seen so far. Call it when the panel becomes the active document again.</summary>
    public void ResetMeters()
    {
        // The shell has no meter yet.
    }

    /// <summary>
    /// Closes the document: an edit that was not saved is taken back from the live mixer (it is applied again from the saved asset),
    /// the mutes and solos the panel set are given back, and a fader gesture still open ends. The history of the context is left to
    /// the caller.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CloseDocument();
    }

    private void CloseDocument()
    {
        var document = _document;
        if (document == null)
        {
            return;
        }

        _document = null;
        document.Changed -= OnDocumentChanged;

        document.EndGesture();
        if (document.IsDirty)
        {
            document.RestoreSavedToLive();
        }

        document.ClearTransientState();
    }

    private void ExecuteCommand(IEditorCommand command)
    {
        // The change is already applied when the command arrives, so the history only records it.
        if (TryGetHistoryContext(out var historyContext))
        {
            EditorHistoryService.Current.Execute(historyContext, command);
        }
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.AudioMixer, _historyContextId);
        return true;
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        bool isDirty = IsDirty;
        if (isDirty != _lastDirty)
        {
            _lastDirty = isDirty;
            DirtyStateChanged?.Invoke(this);
        }

        // A fader being dragged changes no text of the shell: it is refreshed when the gesture ends.
        if (_document is { IsGestureOpen: true })
        {
            return;
        }

        Refresh();
    }

    private void SaveLoadedAsset()
    {
        TrySaveLoadedAsset(out _);
    }

    /// <summary>Brings the controls in line with the document. Called when the document changed, never per frame.</summary>
    private void Refresh()
    {
        if (_root == null)
        {
            return;
        }

        var document = _document;
        if (document == null)
        {
            SetText(_headerText!, "[b]Audio Mixer[/b]");
            SetText(_sourceText!, "No audio mixer loaded.");
            SetVisible(_liveBanner!, false);
            SetVisible(_notHeardBanner!, false);
            SetVisible(_softwareOnlyBanner!, false);
            _saveButton!.IsEnabled = false;
            _reloadButton!.IsEnabled = false;
            _applyButton!.IsEnabled = false;
            RefreshProblems(Array.Empty<string>());
            return;
        }

        var asset = document.Asset;
        string id = (asset.AssetId != Guid.Empty ? asset.AssetId : asset.Id).ToString();

        SetText(_headerText!, $"[b]{EscapeMarkup(asset.Name)}[/b]");
        SetText(_sourceText!, $"Source: {EscapeMarkup(_loadedRelativePath ?? document.RelativePath)}\nId: {id}  Version: {asset.Version}");
        SetText(_statusText!, document.IsDirty
            ? $"Modified {EscapeMarkup(document.RelativePath)}"
            : $"Asset: {EscapeMarkup(document.RelativePath)}");

        SetVisible(_liveBanner!, document.IsLive);
        SetText(_notHeardBanner!, $"This asset is not the project mixer: changes are saved but not heard. Set AudioMixerAsset = {id} in the project file.");
        SetVisible(_notHeardBanner!, !document.IsLive);
        SetVisible(_softwareOnlyBanner!, _audioServiceProvider()?.Backend is not IAudioBusBackend);

        _saveButton!.IsEnabled = true;
        _reloadButton!.IsEnabled = !document.IsDirty;
        _applyButton!.IsEnabled = document.IsLive;

        RefreshProblems(document.Problems);
    }

    private void RefreshProblems(IReadOnlyList<string> problems)
    {
        if (SameProblems(_shownProblems, problems))
        {
            return;
        }

        _shownProblems = problems;
        _problemsHost!.TryRemoveAll();

        if (problems.Count == 0)
        {
            SetVisible(_problemsHost, false);
            return;
        }

        _problemsHost.TryAddChild(new MGTextBlock(_window, $"[b]Problems ({problems.Count})[/b]")
        {
            WrapText = true,
        });

        for (int index = 0; index < problems.Count; index++)
        {
            _problemsHost.TryAddChild(new MGTextBlock(_window, $"- {EscapeMarkup(problems[index])}")
            {
                Opacity = 0.85f,
                WrapText = true,
            });
        }

        SetVisible(_problemsHost, true);
    }

    private static bool SameProblems(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            SetText(_statusText, EscapeMarkup(message));
        }
    }

    private static void SetText(MGTextBlock block, string text)
    {
        if (!string.Equals(block.Text, text, StringComparison.Ordinal))
        {
            block.Text = text;
        }
    }

    private static void SetVisible(MGElement element, bool isVisible)
    {
        Visibility visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        if (element.Visibility != visibility)
        {
            element.Visibility = visibility;
        }
    }

    private MGTextBlock CreateBanner(string text)
    {
        return new MGTextBlock(_window, text)
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.9f,
            WrapText = true,
            Visibility = Visibility.Collapsed,
        };
    }

    private MGButton CreateButton(string label, Action onClick, int width)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = width,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
