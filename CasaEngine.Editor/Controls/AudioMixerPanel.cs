#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Styling;
using CasaEngine.EditorServices.Audio;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Newtonsoft.Json.Linq;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The document panel of one <c>.audioMixer</c> asset (plan T10.5 and T10.6, decisions P49 to P54): the editing shell around an
/// <see cref="AudioMixerDocument"/> (header, banners, Save, Reload, Apply, problem list) and the bus strips below it: a fader, a
/// dB readout, mute and solo, a level meter and, for a custom bus, a delete button, plus a row to add a bus. Clicking the name of
/// a bus of the asset selects it; below the strips, an <see cref="AudioMixerBusDetailView"/> edits its effects, sends and ducking (T10.7).
/// </summary>
/// <remarks>
/// <para>
/// The panel owns the document. Its edits go to the editor history of the context set by <see cref="SetHistoryContextId"/>
/// (one entry per user intent, see <see cref="AudioMixerDocument"/>); the dirty state follows the document. The controls are
/// refreshed when the document raises <see cref="AudioMixerDocument.Changed"/>, never per frame; the strips are rebuilt only when
/// the set or the structure of the buses changed (the asset's, or the live mixer's: a bus the game created appears as a read-only strip).
/// </para>
/// <para>
/// Strips: a bus of the asset is editable (fader 0..1 read in dB, M, S, meter, and a delete button when it is a custom bus); a live
/// bus the asset does not name is read-only (greyed name and meter only); Master and Editor are read-only and show the live volume of
/// their bus. Every other displayed value comes from the asset, never from the live mixer: only the meters (and the read-only Master and
/// Editor volumes) read it. The strip controls carry a <c>Tag</c> naming them (<c>fader:Sfx</c>, <c>mute:Sfx</c>, <c>solo:Sfx</c>,
/// <c>remove:Sfx</c>, <c>db:Sfx</c>).
/// </para>
/// <para>
/// A fader moves through an <see cref="AudioMixerGestureTracker"/>: one burst of changes (a drag, a key repeat, a click on the track)
/// is one history entry, closed by <see cref="Update"/> at the first frame without change where the fader is no longer held. The
/// numeric fields of the detail view group their changes the same way (see <see cref="AudioMixerBusDetailView"/>). While the editor
/// history is suspended (a play session) the faders, the controls of the detail view, Save, Apply, adding and removing a bus are
/// disabled; mute, solo and the meters stay active. A numeric field with changes not written yet counts as unsaved (<see cref="IsDirty"/>),
/// and Save, Apply and Reload write them first (closing the document drops them: they never reached the asset nor the live mixer).
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
    private const string PlayLockBannerText = "Stop play mode to edit the mixer";
    private const string PlayLockRefusalText = AudioMixerBusDetailView.PlayLockRefusalText;

    private const int NameColumnWidth = 130;
    private const int ReadoutColumnWidth = 66;
    private const int ButtonColumnWidth = 26;
    private const int IndentPerDepth = 12;
    private const float ReadOnlyOpacity = 0.55f;

    /// <summary>What a volume that was never shown is compared against: no valid volume is negative.</summary>
    private const float NeverShownVolume = -1f;

    private readonly MGWindow _window;
    private readonly Func<AudioService?> _audioServiceProvider;
    private readonly AudioProfilerModel _meterModel;
    private readonly AudioMixerBusDetailView _detailView;
    private readonly List<StripRow> _strips = new();
    private readonly List<StripSpec> _builtSpecs = new();
    private readonly List<StripSpec> _wantedSpecs = new();
    private readonly List<string> _parentChoices = new();

    private MGDockPanel? _root;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGTextBlock? _liveBanner;
    private MGTextBlock? _notHeardBanner;
    private MGTextBlock? _softwareOnlyBanner;
    private MGTextBlock? _playLockBanner;
    private MGButton? _saveButton;
    private MGButton? _reloadButton;
    private MGButton? _applyButton;
    private MGStackPanel? _problemsHost;
    private MGStackPanel? _bodyHost;
    private MGStackPanel? _stripsHost;
    private MGStackPanel? _addRow;
    private MGTextBox? _addNameBox;
    private MGComboBox<string>? _addParentCombo;
    private MGButton? _addButton;
    private MGTextBlock? _capacityHint;

    private AudioMixerDocument? _document;
    private AudioMixerGestureTracker? _gestureTracker;
    private AudioMixerAssetApplier? _liveApplier;
    private string? _loadedRelativePath;
    private string? _loadedFullPath;
    private string? _historyContextId;
    private string? _selectedBus;
    private IReadOnlyList<string> _shownProblems = Array.Empty<string>();
    private AudioMeterTrack[]? _builtTracks;
    private int _builtLiveBusCount = -2;
    private bool _isSyncingControls;
    private bool _isPlayLocked;
    private bool _recordDespiteSuspension;
    private bool _lastDirty;
    private bool _isDisposed;

    /// <param name="window">The window the panel's controls belong to.</param>
    /// <param name="audioServiceProvider">Gives the audio service of the editor, or null while there is none.</param>
    public AudioMixerPanel(MGWindow window, Func<AudioService?> audioServiceProvider)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _audioServiceProvider = audioServiceProvider ?? throw new ArgumentNullException(nameof(audioServiceProvider));
        _meterModel = new AudioProfilerModel(_audioServiceProvider);
        _detailView = new AudioMixerBusDetailView(_window, _audioServiceProvider, SetStatus);
        _isPlayLocked = EditorHistoryService.Current.IsSuspended;
    }

    /// <summary>The document being edited, null until <see cref="LoadAsset"/>.</summary>
    public AudioMixerDocument? Document => _document;

    /// <summary>The detail of the selected bus: its effects, sends and ducking.</summary>
    internal AudioMixerBusDetailView DetailView => _detailView;

    /// <summary>The bus whose detail is shown (the one whose name was clicked), or null.</summary>
    internal string? SelectedBus => _selectedBus;

    /// <summary>The meters the strips draw: the panel's own model, with its own meter cursor (the Audio panel keeps its own).</summary>
    internal AudioProfilerModel MeterModel => _meterModel;

    public string? LoadedRelativePath => _loadedRelativePath;

    /// <summary>True when the document has unsaved changes, or a numeric field has changes the document does not hold yet (they are written by Save).</summary>
    public bool IsDirty => IsDocumentDirty || _detailView.HasPendingEdit;

    private bool IsDocumentDirty => _document?.IsDirty ?? false;

    /// <summary>Raised when the dirty state of the document changed.</summary>
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

        // No gesture is open here, so the session state can be read afresh.
        _isPlayLocked = EditorHistoryService.Current.IsSuspended;
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
        _gestureTracker = new AudioMixerGestureTracker(_document);
        _detailView.SetDocument(_document);

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

        if (EditorHistoryService.Current.IsSuspended)
        {
            SetStatus("Mixer edits are disabled during a play session.");
            return false;
        }

        // A fader gesture, or a numeric field, still open is a change that is not saved yet.
        _gestureTracker?.End();
        _detailView.FlushPending();
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

        _detailView.FlushPending();
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

        _detailView.FlushPending();
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
        _playLockBanner = CreateBanner(PlayLockBannerText);

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

        _stripsHost = new MGStackPanel(_window, Orientation.Vertical);
        _bodyHost.TryAddChild(_stripsHost);
        _addRow = CreateAddRow();
        _bodyHost.TryAddChild(_addRow);
        _bodyHost.TryAddChild(_detailView.Root);

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
        _root.TryAddChild(_playLockBanner, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        Refresh();
        return _root;
    }

    /// <summary>
    /// Reads the meters from the audio service, follows the play session (see the remarks of the class), closes a fader gesture that
    /// is over, gives the detail view its frame (it closes a numeric field burst that is over) and follows the live mixer (a bus the
    /// game created, the volume of Master and Editor). Allocation free while nothing changes. Call it on every editor frame while the
    /// panel is the active document.
    /// </summary>
    /// <param name="elapsedSeconds">The time since the previous call.</param>
    public void Update(float elapsedSeconds)
    {
        _meterModel.ReadMeters(elapsedSeconds);

        if (_document == null || _stripsHost == null)
        {
            return;
        }

        UpdatePlayLock();
        TickGesture();
        _detailView.Update(elapsedSeconds);
        RefreshLiveStructure();
        RefreshLiveVolumes();
    }

    /// <summary>Forgets the levels seen so far. Call it when the panel becomes the active document again.</summary>
    public void ResetMeters()
    {
        _meterModel.Reset();
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
        _detailView.Dispose();
    }

    private void CloseDocument()
    {
        var document = _document;
        if (document == null)
        {
            return;
        }

        var tracker = _gestureTracker;
        _document = null;
        _gestureTracker = null;
        document.Changed -= OnDocumentChanged;

        // A numeric field with changes not written yet drops them: nothing of them reached the asset nor the live mixer.
        _detailView.SetDocument(null);

        tracker?.End();
        document.EndGesture();
        if (document.IsDirty)
        {
            document.RestoreSavedToLive();
        }

        document.ClearTransientState();
    }

    private void ExecuteCommand(IEditorCommand command)
    {
        if (!TryGetHistoryContext(out var historyContext))
        {
            return;
        }

        // The change is already applied when the command arrives, so the history only records it.
        if (_recordDespiteSuspension || _detailView.IsCommittingBurst)
        {
            // The fader gesture (or the numeric field burst) a play session interrupted: the service refuses commands from now on, and
            // the entry would be lost while the document stays modified. The change was made before the session started, so it is
            // recorded.
            EditorHistoryService.Current.GetOrCreate(historyContext).Execute(command);
            return;
        }

        EditorHistoryService.Current.Execute(historyContext, command);
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
        bool isDirty = IsDocumentDirty;
        if (isDirty != _lastDirty)
        {
            _lastDirty = isDirty;
            DirtyStateChanged?.Invoke(this);
        }

        // A fader being dragged changes no text of the shell: it is refreshed when the gesture ends. The strip of the bus follows at once,
        // without anything being rebuilt: only its fader value and its readout.
        if (_document is { IsGestureOpen: true })
        {
            string? gestureBus = _gestureTracker?.Bus;
            if (gestureBus != null)
            {
                RefreshStripValue(gestureBus);
            }
            else
            {
                RefreshStripValues();
            }

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
            RefreshProblems(Array.Empty<string>());
            _selectedBus = null;
            ClearStrips();
            ApplyEnabledStates();
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

        RefreshProblems(document.Problems);

        // A bus the asset no longer holds (removed, or an undone addition) cannot stay selected.
        if (_selectedBus != null && FindAssetBus(_selectedBus) == null)
        {
            _selectedBus = null;
        }

        RefreshStrips();

        if (string.Equals(_detailView.SelectedBus, _selectedBus, StringComparison.Ordinal))
        {
            _detailView.Refresh();
        }
        else
        {
            _detailView.Show(_selectedBus);
        }

        ApplyEnabledStates();
    }

    /// <summary>
    /// The enabled state of the controls that depend on the document and on the play session: Save and Apply, the faders, adding and
    /// removing a bus are off while the editor history is suspended (P51). Mute, solo and the meters are never disabled.
    /// </summary>
    private void ApplyEnabledStates()
    {
        var document = _document;
        bool hasDocument = document != null;
        bool canEdit = hasDocument && !_isPlayLocked;

        if (_saveButton != null)
        {
            _saveButton.IsEnabled = canEdit;
            // A reload of a live document applies it to the mixer, like Apply: not during a play session.
            _reloadButton!.IsEnabled = canEdit && !document!.IsDirty;
            _applyButton!.IsEnabled = canEdit && document!.IsLive;
        }

        if (_playLockBanner != null)
        {
            SetVisible(_playLockBanner, hasDocument && _isPlayLocked);
        }

        for (int index = 0; index < _strips.Count; index++)
        {
            var strip = _strips[index];
            if (strip.Fader != null)
            {
                strip.Fader.IsEnabled = canEdit;
            }

            if (strip.Remove != null)
            {
                strip.Remove.IsEnabled = canEdit;
            }
        }

        _detailView.SetEditable(canEdit);

        if (_addRow != null)
        {
            bool isFull = hasDocument && _builtSpecs.Count >= document!.BusCapacity;
            SetVisible(_addRow, hasDocument);
            _addButton!.IsEnabled = canEdit && !isFull;
            SetVisible(_capacityHint!, isFull);
            if (isFull)
            {
                SetText(_capacityHint!, $"Bus capacity reached ({document!.BusCapacity})");
            }
        }
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

    // ------------------------------------------------------------------ bus strips

    private enum StripKind
    {
        /// <summary>A bus of the asset: editable.</summary>
        Asset,

        /// <summary>A live bus the asset does not name: read-only.</summary>
        LiveOnly,

        /// <summary>The root bus: read-only, shows its live volume.</summary>
        Master,

        /// <summary>The editor preview bus: read-only, shows its live volume.</summary>
        Editor,
    }

    /// <summary>What one strip is, compared as a whole to know whether the strips must be rebuilt.</summary>
    private readonly record struct StripSpec(string Name, StripKind Kind, int Depth, string Parent);

    private sealed class StripRow
    {
        public StripRow(StripSpec spec)
        {
            Spec = spec;
        }

        public StripSpec Spec { get; }

        public string Name => Spec.Name;

        public MGTextBlock? NameText { get; set; }

        /// <summary>True while the name carries the selection highlight.</summary>
        public bool IsHighlighted { get; set; }

        public MGSlider? Fader { get; set; }

        public MGTextBlock? Readout { get; set; }

        public MGToggleButton? Mute { get; set; }

        public MGToggleButton? Solo { get; set; }

        public MGButton? Remove { get; set; }

        /// <summary>The volume the readout was last built for, <see cref="NeverShownVolume"/> before the first one.</summary>
        public float ShownVolume { get; set; } = NeverShownVolume;
    }

    /// <summary>
    /// Brings the strips in line with the document and the live mixer: rebuilt when the set, the order or the nesting of the buses
    /// changed, or when the live mixer or the meters grew another bus; otherwise only the values are refreshed.
    /// </summary>
    private void RefreshStrips()
    {
        var document = _document;
        if (document == null || _stripsHost == null)
        {
            ClearStrips();
            return;
        }

        BuildWantedSpecs(document);
        int liveCount = _audioServiceProvider()?.Mixer.Buses.Count ?? -1;

        if (!SameSpecs() || liveCount != _builtLiveBusCount || !ReferenceEquals(_builtTracks, _meterModel.Buses))
        {
            RebuildStrips(liveCount);
        }
        else
        {
            RefreshStripValues();
            ApplySelectionHighlight();
        }

        RefreshParentChoices();
    }

    /// <summary>
    /// Selects the bus <paramref name="bus"/> of the asset: its name is highlighted in the strips and its detail is shown below them. False
    /// when the asset has no such bus (a live bus the asset does not name, Master and Editor are not selectable: there is nothing of
    /// theirs to edit).
    /// </summary>
    internal bool SelectBus(string bus)
    {
        var data = _document != null ? FindAssetBus(bus) : null;
        if (data == null)
        {
            return false;
        }

        if (!string.Equals(_selectedBus, data.Name, StringComparison.Ordinal))
        {
            _selectedBus = data.Name;
            ApplySelectionHighlight();
            _detailView.Show(_selectedBus);
        }

        return true;
    }

    /// <summary>Puts the highlight on the name of the selected bus, and takes it off the others. Only writes what changed.</summary>
    private void ApplySelectionHighlight()
    {
        for (int index = 0; index < _strips.Count; index++)
        {
            var strip = _strips[index];
            bool isSelected = _selectedBus != null
                              && strip.Spec.Kind == StripKind.Asset
                              && string.Equals(strip.Name, _selectedBus, StringComparison.OrdinalIgnoreCase);
            if (strip.NameText == null || strip.IsHighlighted == isSelected)
            {
                continue;
            }

            strip.IsHighlighted = isSelected;
            strip.NameText.BackgroundBrush = new VisualStateFillBrush(isSelected
                ? new MGSolidFillBrush(EditorThemePalette.AccentSelection)
                : SolidFillBrushes.Transparent);
        }
    }

    private void ClearStrips()
    {
        _strips.Clear();
        _builtSpecs.Clear();
        _parentChoices.Clear();
        _builtLiveBusCount = -2;
        _builtTracks = null;
        _stripsHost?.TryRemoveAll();
    }

    private bool SameSpecs()
    {
        if (_builtSpecs.Count != _wantedSpecs.Count)
        {
            return false;
        }

        for (int index = 0; index < _builtSpecs.Count; index++)
        {
            if (!_builtSpecs[index].Equals(_wantedSpecs[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lists the strips in the order they are shown: Master, then the tree below it (the buses of the asset in file order, then the
    /// live buses the asset does not name), then Editor. A bus no path from Master reaches (an unknown parent, a cycle: the problem list
    /// says so) still gets its strip.
    /// </summary>
    private void BuildWantedSpecs(AudioMixerDocument document)
    {
        _wantedSpecs.Clear();

        var nodes = new List<StripSpec>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AudioBusNames.Master, AudioBusNames.Editor };

        var assetBuses = document.Asset.Buses;
        for (int index = 0; index < assetBuses.Count; index++)
        {
            var bus = assetBuses[index];
            if (bus != null && !string.IsNullOrWhiteSpace(bus.Name) && known.Add(bus.Name))
            {
                string parent = string.IsNullOrWhiteSpace(bus.Parent) ? AudioBusNames.Master : bus.Parent;
                nodes.Add(new StripSpec(bus.Name, StripKind.Asset, 0, parent));
            }
        }

        var liveBuses = _audioServiceProvider()?.Mixer.Buses;
        if (liveBuses != null)
        {
            for (int index = 0; index < liveBuses.Count; index++)
            {
                var bus = liveBuses[index];
                if (known.Add(bus.Name))
                {
                    nodes.Add(new StripSpec(bus.Name, StripKind.LiveOnly, 0, bus.Parent?.Name ?? AudioBusNames.Master));
                }
            }
        }

        _wantedSpecs.Add(new StripSpec(AudioBusNames.Master, StripKind.Master, 0, string.Empty));

        var emitted = new bool[nodes.Count];
        EmitChildren(nodes, emitted, AudioBusNames.Master, 1);

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!emitted[index])
            {
                emitted[index] = true;
                _wantedSpecs.Add(nodes[index] with { Depth = 1 });
                EmitChildren(nodes, emitted, nodes[index].Name, 2);
            }
        }

        _wantedSpecs.Add(new StripSpec(AudioBusNames.Editor, StripKind.Editor, 1, AudioBusNames.Master));
    }

    private void EmitChildren(List<StripSpec> nodes, bool[] emitted, string parent, int depth)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            if (!emitted[index] && string.Equals(nodes[index].Parent, parent, StringComparison.OrdinalIgnoreCase))
            {
                emitted[index] = true;
                _wantedSpecs.Add(nodes[index] with { Depth = depth });
                EmitChildren(nodes, emitted, nodes[index].Name, depth + 1);
            }
        }
    }

    private void RebuildStrips(int liveCount)
    {
        _builtSpecs.Clear();
        _builtSpecs.AddRange(_wantedSpecs);
        _builtLiveBusCount = liveCount;
        _builtTracks = _meterModel.Buses;
        _strips.Clear();
        _stripsHost!.TryRemoveAll();

        var grid = new MGGrid(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ColumnSpacing = 6,
            RowSpacing = 3,
        };
        grid.AddColumn(GridLength.CreatePixelLength(NameColumnWidth));
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        grid.AddColumn(GridLength.CreatePixelLength(ReadoutColumnWidth));
        grid.AddColumn(GridLength.CreatePixelLength(ButtonColumnWidth));
        grid.AddColumn(GridLength.CreatePixelLength(ButtonColumnWidth));
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        grid.AddColumn(GridLength.CreatePixelLength(ButtonColumnWidth));

        int rowIndex = 0;
        grid.AddRow(GridLength.Auto);
        grid.TryAddChild(rowIndex, 0, CreateHeaderText("Bus", HorizontalAlignment.Left));
        grid.TryAddChild(rowIndex, 1, CreateHeaderText("Volume", HorizontalAlignment.Left));
        grid.TryAddChild(rowIndex, 5, CreateHeaderText("Level (dBFS)", HorizontalAlignment.Left));
        rowIndex++;

        for (int index = 0; index < _builtSpecs.Count; index++)
        {
            AddStripRow(grid, rowIndex++, _builtSpecs[index]);
        }

        _stripsHost.TryAddChild(grid);
        RefreshStripValues();
        RefreshLiveVolumes();
        ApplySelectionHighlight();
    }

    private void AddStripRow(MGGrid grid, int rowIndex, StripSpec spec)
    {
        grid.AddRow(GridLength.Auto);

        var strip = new StripRow(spec);
        var assetBus = spec.Kind == StripKind.Asset ? FindAssetBus(spec.Name) : null;
        bool isEditable = assetBus != null;

        var nameText = new MGTextBlock(_window, EscapeMarkup(spec.Name))
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(spec.Depth * IndentPerDepth, 0, 0, 0),
            Opacity = isEditable ? 1f : ReadOnlyOpacity,
            WrapText = false,
        };

        if (isEditable)
        {
            // Clicking the name of a bus of the asset selects it: its effects and sends show below the strips.
            nameText.Tag = "bus:" + spec.Name;
            nameText.HorizontalAlignment = HorizontalAlignment.Stretch;
            nameText.MouseHandler.LMBClickedInside += (_, _) => SelectBus(spec.Name);
        }

        strip.NameText = nameText;
        grid.TryAddChild(rowIndex, 0, nameText);

        if (isEditable)
        {
            var fader = new MGSlider(_window, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, assetBus!.Volume)
            {
                MinWidth = 100,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "fader:" + spec.Name,
            };
            fader.ValueChanged += (_, e) => OnFaderChanged(strip, e.NewValue);
            strip.Fader = fader;
            grid.TryAddChild(rowIndex, 1, fader);
        }

        if (isEditable || spec.Kind is StripKind.Master or StripKind.Editor)
        {
            strip.Readout = new MGTextBlock(_window, string.Empty)
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                WrapText = false,
                Tag = "db:" + spec.Name,
            };
            grid.TryAddChild(rowIndex, 2, strip.Readout);
        }

        if (isEditable)
        {
            strip.Mute = CreateToggle("M", "mute:" + spec.Name, isMuted => _document?.SetMute(spec.Name, isMuted));
            strip.Solo = CreateToggle("S", "solo:" + spec.Name, isSoloed => _document?.SetSolo(spec.Name, isSoloed));
            grid.TryAddChild(rowIndex, 3, strip.Mute);
            grid.TryAddChild(rowIndex, 4, strip.Solo);
        }

        grid.TryAddChild(rowIndex, 5, new AudioMeterControl(_window, GetMeterTrack(spec))
        {
            MinWidth = 80,
        });

        if (isEditable && !IsDefaultBus(spec.Name))
        {
            strip.Remove = CreateButton("X", () => TryRemoveBus(spec.Name), ButtonColumnWidth);
            strip.Remove.Tag = "remove:" + spec.Name;
            grid.TryAddChild(rowIndex, 6, strip.Remove);
        }

        _strips.Add(strip);
    }

    /// <summary>The meter of a bus: the live one when the model has it, otherwise a silent one (the bus is not live, or the backend does not measure).</summary>
    private AudioMeterTrack GetMeterTrack(StripSpec spec)
    {
        var tracks = _meterModel.Buses;
        for (int index = 0; index < tracks.Length; index++)
        {
            if (string.Equals(tracks[index].Name, spec.Name, StringComparison.OrdinalIgnoreCase))
            {
                return tracks[index];
            }
        }

        return new AudioMeterTrack(spec.Name, spec.Depth, -1, isOutput: false);
    }

    private AudioMixerBusData? FindAssetBus(string name)
    {
        var buses = _document?.Asset.Buses;
        if (buses == null)
        {
            return null;
        }

        for (int index = 0; index < buses.Count; index++)
        {
            var bus = buses[index];
            if (bus != null && string.Equals(bus.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return bus;
            }
        }

        return null;
    }

    private StripRow? FindStrip(string name)
    {
        for (int index = 0; index < _strips.Count; index++)
        {
            if (string.Equals(_strips[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return _strips[index];
            }
        }

        return null;
    }

    private static bool IsDefaultBus(string name)
        => string.Equals(name, AudioBusNames.Music, StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, AudioBusNames.Sfx, StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, AudioBusNames.Voice, StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, AudioBusNames.Ui, StringComparison.OrdinalIgnoreCase);

    /// <summary>A volume as the fader readout shows it: dB with a tenth of precision (the scale of the meters), "-inf dB" at silence.</summary>
    internal static string FormatDb(float volume)
    {
        return AudioMeterTrack.FormatTenths(AudioMeterTrack.ToTenths(AudioMeterScale.ToDb(volume))) + " dB";
    }

    /// <summary>
    /// Writes the values of the document into the controls of the editable strips (fader, readout, M and S) without raising their change
    /// events as edits. Allocates only the text of a readout whose value changed.
    /// </summary>
    private void RefreshStripValues()
    {
        var document = _document;
        if (document == null)
        {
            return;
        }

        bool wasSyncing = _isSyncingControls;
        _isSyncingControls = true;
        try
        {
            for (int index = 0; index < _strips.Count; index++)
            {
                ApplyStripValues(document, _strips[index]);
            }
        }
        finally
        {
            _isSyncingControls = wasSyncing;
        }
    }

    /// <summary>The same for the strip of one bus: what a fader gesture needs while the fader moves.</summary>
    private void RefreshStripValue(string bus)
    {
        var document = _document;
        var strip = FindStrip(bus);
        if (document == null || strip == null)
        {
            return;
        }

        bool wasSyncing = _isSyncingControls;
        _isSyncingControls = true;
        try
        {
            ApplyStripValues(document, strip);
        }
        finally
        {
            _isSyncingControls = wasSyncing;
        }
    }

    private void ApplyStripValues(AudioMixerDocument document, StripRow strip)
    {
        var bus = strip.Spec.Kind == StripKind.Asset ? FindAssetBus(strip.Name) : null;
        if (bus == null)
        {
            return;
        }

        float volume = bus.Volume;
        if (strip.Fader != null && strip.Fader.Value != volume)
        {
            strip.Fader.Value = volume;
        }

        if (strip.Readout != null && strip.ShownVolume != volume)
        {
            strip.Readout.Text = FormatDb(volume);
            strip.ShownVolume = volume;
        }

        if (strip.Mute != null)
        {
            bool isMuted = document.IsMuted(strip.Name);
            if (strip.Mute.IsChecked != isMuted)
            {
                strip.Mute.IsChecked = isMuted;
            }
        }

        if (strip.Solo != null)
        {
            bool isSoloed = document.IsSoloed(strip.Name);
            if (strip.Solo.IsChecked != isSoloed)
            {
                strip.Solo.IsChecked = isSoloed;
            }
        }
    }

    /// <summary>Shows the live volume of Master and Editor, the two read-only strips that read the live mixer. Allocation free while the volumes stay.</summary>
    private void RefreshLiveVolumes()
    {
        AudioMixer? mixer = null;

        for (int index = 0; index < _strips.Count; index++)
        {
            var strip = _strips[index];
            if (strip.Readout == null || strip.Spec.Kind is not (StripKind.Master or StripKind.Editor))
            {
                continue;
            }

            mixer ??= _audioServiceProvider()?.Mixer;
            float volume = float.NaN;
            if (mixer != null && mixer.TryGetBus(strip.Name, out var bus))
            {
                volume = bus.Volume;
            }

            if (volume.Equals(strip.ShownVolume))
            {
                continue;
            }

            strip.ShownVolume = volume;
            strip.Readout.Text = float.IsNaN(volume) ? "n/a" : FormatDb(volume);
        }
    }

    /// <summary>Rebuilds the strips when the live mixer grew a bus or the meters changed their set. Waits for the end of a fader gesture.</summary>
    private void RefreshLiveStructure()
    {
        int liveCount = _audioServiceProvider()?.Mixer.Buses.Count ?? -1;
        if (liveCount == _builtLiveBusCount && ReferenceEquals(_builtTracks, _meterModel.Buses))
        {
            return;
        }

        // A rebuild would replace the fader under the mouse: the strips stay as they are until the gesture is over.
        if (_document is { IsGestureOpen: true })
        {
            return;
        }

        RefreshStrips();

        // The choices of the detail view (the live buses) changed too, unless a numeric field is being typed in: its next write refreshes them.
        if (!_detailView.HasPendingEdit)
        {
            _detailView.Refresh();
        }

        ApplyEnabledStates();
    }

    private MGTextBlock CreateHeaderText(string text, HorizontalAlignment alignment)
    {
        return new MGTextBlock(_window, text)
        {
            HorizontalAlignment = alignment,
            Opacity = 0.7f,
            WrapText = false,
        };
    }

    private MGToggleButton CreateToggle(string label, string tag, Action<bool> onChanged)
    {
        var toggle = new MGToggleButton(_window)
        {
            PreferredWidth = ButtonColumnWidth,
            Tag = tag,
        };
        toggle.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        toggle.OnCheckStateChanged += (_, e) =>
        {
            // The strips writing the state of the document into the toggle is not a click.
            if (!_isSyncingControls)
            {
                onChanged(e.NewValue);
            }
        };
        return toggle;
    }

    // ------------------------------------------------------------------ fader gestures and the play session

    /// <summary>A fader moved: through the tracker, so that the burst of changes is one history entry.</summary>
    private void OnFaderChanged(StripRow strip, float value)
    {
        if (_isSyncingControls || _document == null || _gestureTracker == null)
        {
            return;
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            // The faders are disabled during a play session; this is a change that raced the first frame of the session.
            RefreshStripValues();
            return;
        }

        _gestureTracker.Change(strip.Name, value);
    }

    /// <summary>Gives the tracker the frame: the gesture closes when nothing changed and the fader that opened it is no longer held.</summary>
    private void TickGesture()
    {
        var tracker = _gestureTracker;
        if (tracker == null)
        {
            return;
        }

        bool isHeld = false;
        string? bus = tracker.Bus;
        if (bus != null)
        {
            var fader = FindStrip(bus)?.Fader;
            isHeld = fader != null && (fader.IsDraggingThumb || fader.IsLMBPressed);
        }

        tracker.Tick(isHeld);
    }

    /// <summary>
    /// Follows the play session (the editor history is suspended): a gesture still open ends at once, and the controls that edit through
    /// the history are disabled until the session is over.
    /// </summary>
    private void UpdatePlayLock()
    {
        bool isLocked = EditorHistoryService.Current.IsSuspended;
        if (isLocked == _isPlayLocked)
        {
            return;
        }

        _isPlayLocked = isLocked;

        if (isLocked)
        {
            _recordDespiteSuspension = true;
            try
            {
                _gestureTracker?.End();
                _detailView.FlushPending();
            }
            finally
            {
                _recordDespiteSuspension = false;
            }
        }

        ApplyEnabledStates();
    }

    // ------------------------------------------------------------------ add and remove a bus

    private MGStackPanel CreateAddRow()
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
        };

        row.TryAddChild(new MGTextBlock(_window, "Add bus")
        {
            VerticalAlignment = VerticalAlignment.Center,
        });

        _addNameBox = new MGTextBox(_window, CharacterLimit: 64)
        {
            PreferredWidth = 150,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.TryAddChild(_addNameBox);

        row.TryAddChild(new MGTextBlock(_window, "under")
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7f,
        });

        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 120,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(EscapeMarkup(item));
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, EscapeMarkup(item))
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _addParentCombo = combo;
        row.TryAddChild(combo);

        _addButton = CreateButton("Add", OnAddClicked, 60);
        row.TryAddChild(_addButton);

        _capacityHint = new MGTextBlock(_window, string.Empty)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.7f,
            Visibility = Visibility.Collapsed,
        };
        row.TryAddChild(_capacityHint);

        return row;
    }

    /// <summary>The parents the new bus can go under: Master, then the buses of the asset.</summary>
    private void RefreshParentChoices()
    {
        var combo = _addParentCombo;
        if (combo == null || _document == null)
        {
            return;
        }

        bool isSame = _parentChoices.Count > 0 && _parentChoices[0] == AudioBusNames.Master;
        int position = 1;
        for (int index = 0; isSame && index < _wantedSpecs.Count; index++)
        {
            if (_wantedSpecs[index].Kind != StripKind.Asset)
            {
                continue;
            }

            isSame = position < _parentChoices.Count && _parentChoices[position] == _wantedSpecs[index].Name;
            position++;
        }

        if (isSame && position == _parentChoices.Count)
        {
            return;
        }

        string selected = combo.SelectedItem ?? AudioBusNames.Master;

        _parentChoices.Clear();
        _parentChoices.Add(AudioBusNames.Master);
        for (int index = 0; index < _wantedSpecs.Count; index++)
        {
            if (_wantedSpecs[index].Kind == StripKind.Asset)
            {
                _parentChoices.Add(_wantedSpecs[index].Name);
            }
        }

        combo.SetItemsSource(new List<string>(_parentChoices));
        combo.SelectedItem = _parentChoices.Contains(selected) ? selected : AudioBusNames.Master;
    }

    private void OnAddClicked()
    {
        TryAddBus(_addNameBox?.Text ?? string.Empty, _addParentCombo?.SelectedItem ?? AudioBusNames.Master);
    }

    /// <summary>
    /// Adds a custom bus to the asset under <paramref name="parent"/>, as one history entry; what the user typed is cleared. A refusal
    /// (reserved or duplicate name, unknown parent, capacity) shows in the status line. Nothing happens during a play session.
    /// </summary>
    internal bool TryAddBus(string name, string parent)
    {
        if (_document == null)
        {
            return false;
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            SetStatus(PlayLockRefusalText);
            return false;
        }

        if (!_document.TryAddBus(name, parent, out string error))
        {
            SetStatus(error);
            return false;
        }

        _addNameBox?.SetText(string.Empty);
        SetStatus($"Added bus '{name.Trim()}'");
        return true;
    }

    /// <summary>
    /// Removes a custom bus of the asset (see <see cref="AudioMixerDocument.TryRemoveBus"/>): one history entry; the live bus stays until the
    /// next start and then shows as a read-only strip. A refusal shows in the status line. Nothing happens during a play session.
    /// </summary>
    internal bool TryRemoveBus(string name)
    {
        if (_document == null)
        {
            return false;
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            SetStatus(PlayLockRefusalText);
            return false;
        }

        if (!_document.TryRemoveBus(name, out string error))
        {
            SetStatus(error);
            return false;
        }

        SetStatus($"Removed bus '{name}' from the asset");
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

    internal static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}
