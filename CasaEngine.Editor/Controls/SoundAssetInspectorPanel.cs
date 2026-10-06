using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using CasaEngine.Framework.Configuration;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Newtonsoft.Json.Linq;
using Thickness = MonoGame.Extended.Thickness;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Inspector for a <c>.sound</c> asset: audio file, variation audio files, volume, pitch, volume and
/// pitch variation ranges, voice priority, loop, target bus, streaming, plus a preview.
/// </summary>
/// <remarks>
/// The preview is routed to the Editor bus, never to the game buses: it must not be silenced by
/// the game mix, and it must survive the end of a play session. It plays with priority 0, so it
/// never steals a voice from the game. Variations and priority only apply to a non-streaming asset.
/// </remarks>
public sealed class SoundAssetInspectorPanel : IDisposable
{
    private static readonly string[] BusChoices =
    {
        AudioBusNames.Sfx,
        AudioBusNames.Music,
        AudioBusNames.Voice,
        AudioBusNames.Ui,
    };

    private readonly MGWindow _window;
    private readonly HostedEditorGameAdapter _editorRuntime;

    private MGDockPanel _root;
    private MGTextBlock _headerText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGStackPanel _fieldStack;

    private SoundAsset _soundAsset;
    private string _loadedRelativePath;
    private bool _isDirty;
    private bool _suppressControlCallbacks;

    private AudioVoiceHandle _previewVoice = AudioVoiceHandle.None;
    private MusicTrackHandle _previewTrack = MusicTrackHandle.None;

    public SoundAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    internal SoundAssetInspectorPanel(MGWindow window, HostedEditorGameAdapter editorRuntime)
    {
        _window = window;
        _editorRuntime = editorRuntime;
    }

    public SoundAsset LoadedSoundAsset => _soundAsset;

    public string LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    public event Action<SoundAssetInspectorPanel> DirtyStateChanged;

    public static bool TryLoadAsset(string fullPath, out SoundAsset soundAsset)
    {
        soundAsset = new SoundAsset();

        if (!File.Exists(fullPath)
            || !Path.GetExtension(fullPath).Equals(Constants.FileNameExtensions.Sound, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            soundAsset.Load(document);
            soundAsset.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(soundAsset.FileName)
                            ?? AssetCatalog.GetByFileName(soundAsset.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                soundAsset.AssetId = assetInfo.Id;
            }

            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"Cannot load sound asset '{fullPath}'", exception));
            return false;
        }
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Sound Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No sound asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .sound asset from the Content Browser.")
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
        toolbar.TryAddChild(CreateButton("Play", PlayPreview));
        toolbar.TryAddChild(CreateButton("Stop", StopPreview));
        toolbar.TryAddChild(CreateButton("Save", SaveLoadedAsset));
        toolbar.TryAddChild(CreateButton("Reload", () => ReloadFromDisk()));

        _fieldStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_fieldStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public void LoadAsset(SoundAsset soundAsset, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(soundAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        StopPreview();

        _soundAsset = soundAsset;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        SetDirty(false);
        RefreshInspector();
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadAsset(fullPath, out var soundAsset))
        {
            SetStatus($"Cannot reload {_loadedRelativePath}");
            return false;
        }

        LoadAsset(soundAsset, fullPath);
        SetStatus($"Reloaded {_loadedRelativePath}");
        return true;
    }

    public bool TrySaveLoadedAsset(out string errorMessage)
    {
        errorMessage = null;

        if (_soundAsset == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No sound asset is loaded.";
            return false;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _soundAsset, EditorAssetSaveSource.SoundEditorPanel);
            SetDirty(false);
            SetStatus($"Saved {_loadedRelativePath}");
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            errorMessage = exception.Message;
            SetStatus($"Cannot save {_loadedRelativePath}: {exception.Message}");
            return false;
        }
    }

    /// <summary>Plays the asset on the Editor bus, so the game mix cannot silence the preview.</summary>
    public void PlayPreview()
    {
        var audioService = GetAudioService();
        if (audioService == null || _soundAsset == null)
        {
            SetStatus("No audio service available for the preview.");
            return;
        }

        StopPreview();

        if (_soundAsset.IsStreaming)
        {
            // The Editor bus is forced here rather than on the asset, which keeps its own bus.
            var previewAsset = CreatePreviewCopy(_soundAsset);
            _previewTrack = audioService.Music.Play(previewAsset);
            SetStatus(_previewTrack.IsValid ? "Previewing (streamed)" : "The stream could not be started.");
            return;
        }

        // Priority 0: the preview neither steals a game voice nor can be stolen by one.
        _previewVoice = audioService.PlaySound(
            _soundAsset,
            new SoundPlaybackOverrides(busName: AudioBusNames.Editor) { Priority = 0 });

        SetStatus(_previewVoice.IsValid ? "Previewing" : "The sound could not be played.");
    }

    public void StopPreview()
    {
        var audioService = GetAudioService();
        if (audioService == null)
        {
            return;
        }

        if (_previewVoice.IsValid)
        {
            audioService.Stop(_previewVoice);
            _previewVoice = AudioVoiceHandle.None;
        }

        if (_previewTrack.IsValid)
        {
            audioService.Music.Stop(_previewTrack);
            _previewTrack = MusicTrackHandle.None;
        }
    }

    public void Dispose()
    {
        StopPreview();
    }

    private static SoundAsset CreatePreviewCopy(SoundAsset source)
    {
        return new SoundAsset
        {
            Name = source.Name,
            AudioFileAssetId = source.AudioFileAssetId,
            Volume = source.Volume,
            Pitch = source.Pitch,
            IsLooped = source.IsLooped,
            IsStreaming = source.IsStreaming,
            BusName = AudioBusNames.Editor,
        };
    }

    private AudioService GetAudioService()
    {
        return _editorRuntime?.AudioSystemComponent?.Service;
    }

    /// <summary>Appends an empty variation file entry to the asset and rebuilds the inspector.</summary>
    internal void AddVariationFile()
    {
        if (_soundAsset == null)
        {
            return;
        }

        _soundAsset.VariationAudioFileAssetIds.Add(Guid.Empty);
        SetDirty(true);
        RefreshInspector();
    }

    /// <summary>Removes the variation file entry at <paramref name="index"/> (ignored when out of range) and rebuilds the inspector.</summary>
    internal void RemoveVariationFile(int index)
    {
        if (_soundAsset == null || index < 0 || index >= _soundAsset.VariationAudioFileAssetIds.Count)
        {
            return;
        }

        _soundAsset.VariationAudioFileAssetIds.RemoveAt(index);
        SetDirty(true);
        RefreshInspector();
    }

    private void SaveLoadedAsset()
    {
        TrySaveLoadedAsset(out _);
    }

    private void RefreshInspector()
    {
        if (_fieldStack == null)
        {
            return;
        }

        _fieldStack.TryRemoveAll();

        if (_soundAsset == null)
        {
            _headerText.Text = "[b]Sound Inspector[/b]";
            _sourceText.Text = "No sound asset loaded.";
            return;
        }

        _headerText.Text = $"[b]{_soundAsset.Name}[/b]";
        _sourceText.Text = _loadedRelativePath ?? string.Empty;

        _suppressControlCallbacks = true;

        _fieldStack.TryAddChild(CreateAudioFileRow());
        for (var i = 0; i < _soundAsset.VariationAudioFileAssetIds.Count; i++)
        {
            _fieldStack.TryAddChild(CreateVariationFileRow(i));
        }

        _fieldStack.TryAddChild(CreateAddVariationFileRow());
        _fieldStack.TryAddChild(CreateVolumeRow());
        _fieldStack.TryAddChild(CreatePitchRow());
        _fieldStack.TryAddChild(CreateVolumeVariationRow());
        _fieldStack.TryAddChild(CreatePitchVariationRow());
        _fieldStack.TryAddChild(CreatePriorityRow());
        _fieldStack.TryAddChild(CreateHelpText("Priority 0 = none: never stolen, never steals."));
        _fieldStack.TryAddChild(CreateHelpText("Variations and priority are ignored for a streaming asset."));
        _fieldStack.TryAddChild(CreateCheckBoxRow("Looped", _soundAsset.IsLooped, value =>
        {
            _soundAsset.IsLooped = value;
            SetDirty(true);
        }));
        _fieldStack.TryAddChild(CreateCheckBoxRow(
            "Streaming (decoded on the fly, for music)",
            _soundAsset.IsStreaming,
            value =>
            {
                _soundAsset.IsStreaming = value;
                SetDirty(true);
            }));
        _fieldStack.TryAddChild(CreateBusRow());

        _suppressControlCallbacks = false;
    }

    private MGElement CreateAudioFileRow()
    {
        var row = CreateRow("Audio file");
        row.TryAddChild(CreateAudioFileSelector(
            _soundAsset.AudioFileAssetId,
            assetId => _soundAsset.AudioFileAssetId = assetId));
        return row;
    }

    private MGElement CreateVariationFileRow(int index)
    {
        var row = CreateRow($"Variation {index + 1}");
        row.TryAddChild(CreateAudioFileSelector(
            _soundAsset.VariationAudioFileAssetIds[index],
            assetId =>
            {
                if (index < _soundAsset.VariationAudioFileAssetIds.Count)
                {
                    _soundAsset.VariationAudioFileAssetIds[index] = assetId;
                }
            }));
        row.TryAddChild(CreateButton("Remove", () => RemoveVariationFile(index)));
        return row;
    }

    private MGElement CreateAddVariationFileRow()
    {
        var row = CreateRow("Variations");
        row.TryAddChild(CreateButton("Add variation file", AddVariationFile, preferredWidth: null));
        return row;
    }

    /// <summary>
    /// Selector of an audio file, limited to the formats the engine can decode. <paramref name="onChanged"/> writes
    /// the asset, then the asset is marked dirty, unless the inspector is being rebuilt.
    /// </summary>
    private AssetSelector CreateAudioFileSelector(Guid assetId, Action<Guid> onChanged)
    {
        var selector = new AssetSelector(_window)
        {
            AssetId = assetId,
            // Only the formats the engine can actually decode.
            Filter = assetInfo => assetInfo.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                || assetInfo.FileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase),
        };
        selector.AssetChanged += (_, selectedAssetId) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            onChanged(selectedAssetId);
            SetDirty(true);
        };

        return selector;
    }

    private MGElement CreateVolumeRow()
    {
        var row = CreateRow("Volume");
        var field = new NumericField(_window, string.Empty, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, 0.05f)
        {
            Value = _soundAsset.Volume,
        };
        field.ValueChanged += (_, value) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            _soundAsset.Volume = value;
            SetDirty(true);
        };

        row.TryAddChild(field);
        return row;
    }

    private MGElement CreatePitchRow()
    {
        var row = CreateRow("Pitch");
        var field = new NumericField(_window, string.Empty, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch, 0.05f)
        {
            Value = _soundAsset.Pitch,
        };
        field.ValueChanged += (_, value) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            _soundAsset.Pitch = value;
            SetDirty(true);
        };

        row.TryAddChild(field);
        return row;
    }

    private MGElement CreateVolumeVariationRow()
    {
        var row = CreateRow("Volume variation");
        row.TryAddChild(CreateNumericField(
            "Min",
            AudioVoiceParameters.MinVolume,
            AudioVoiceParameters.MaxVolume,
            0.05f,
            _soundAsset.VariationVolumeMin,
            value => _soundAsset.VariationVolumeMin = MathF.Round(value, 2)));
        row.TryAddChild(CreateNumericField(
            "Max",
            AudioVoiceParameters.MinVolume,
            AudioVoiceParameters.MaxVolume,
            0.05f,
            _soundAsset.VariationVolumeMax,
            value => _soundAsset.VariationVolumeMax = MathF.Round(value, 2)));
        return row;
    }

    private MGElement CreatePitchVariationRow()
    {
        var row = CreateRow("Pitch variation");
        row.TryAddChild(CreateNumericField(
            "Min",
            AudioVoiceParameters.MinPitch,
            AudioVoiceParameters.MaxPitch,
            0.05f,
            _soundAsset.VariationPitchMin,
            value => _soundAsset.VariationPitchMin = MathF.Round(value, 2)));
        row.TryAddChild(CreateNumericField(
            "Max",
            AudioVoiceParameters.MinPitch,
            AudioVoiceParameters.MaxPitch,
            0.05f,
            _soundAsset.VariationPitchMax,
            value => _soundAsset.VariationPitchMax = MathF.Round(value, 2)));
        return row;
    }

    private MGElement CreatePriorityRow()
    {
        var row = CreateRow("Priority");
        row.TryAddChild(CreateNumericField(
            string.Empty,
            0f,
            SoundAsset.MaxPriority,
            1f,
            _soundAsset.Priority,
            value => _soundAsset.Priority = (int)MathF.Round(value)));
        return row;
    }

    /// <summary>
    /// Numeric field writing the asset through <paramref name="applyValue"/>, then marking it dirty, unless the
    /// inspector is being rebuilt. The float field drifts (0.35 becomes 0.35000002): callers round what they write.
    /// </summary>
    private NumericField CreateNumericField(string label, float min, float max, float step, float value, Action<float> applyValue)
    {
        var field = new NumericField(_window, label, min, max, step)
        {
            Value = value,
        };
        field.ValueChanged += (_, newValue) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            applyValue(newValue);
            SetDirty(true);
        };

        return field;
    }

    private MGElement CreateBusRow()
    {
        var row = CreateRow("Bus");

        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 140,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item);
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, item)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        combo.SetItemsSource(BusChoices);
        combo.SelectedItem = _soundAsset.BusName;
        combo.SelectedItemChanged += (_, args) =>
        {
            if (_suppressControlCallbacks || string.IsNullOrWhiteSpace(args.NewValue))
            {
                return;
            }

            _soundAsset.BusName = args.NewValue;
            SetDirty(true);
        };

        row.TryAddChild(combo);
        return row;
    }

    private MGStackPanel CreateRow(string label)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 6 };
        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return row;
    }

    private MGTextBlock CreateHelpText(string text)
    {
        return new MGTextBlock(_window, text)
        {
            Opacity = 0.7f,
            WrapText = true,
        };
    }

    private MGElement CreateCheckBoxRow(string label, bool isChecked, Action<bool> onChanged)
    {
        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = isChecked,
        };
        checkBox.SetContent(new MGTextBlock(_window, label)
        {
            VerticalAlignment = VerticalAlignment.Center,
        });
        checkBox.OnCheckStateChanged += (_, args) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            onChanged(args.NewValue ?? false);
        };

        return checkBox;
    }

    private MGButton CreateButton(string label, Action onClick, int? preferredWidth = 84)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = preferredWidth,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
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

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.Text = status;
        }
    }
}
