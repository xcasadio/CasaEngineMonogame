using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Audio;
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
/// pitch variation ranges, voice priority, loop, target bus, streaming, spatial mode, distance model, distances, rolloff
/// and Doppler factor, the count of parameter bindings, plus a preview.
/// </summary>
/// <remarks>
/// The preview is routed to the Editor bus, never to the game buses: it must not be silenced by
/// the game mix, and it must survive the end of a play session. It plays with priority 0, so it
/// never steals a voice from the game. Variations and priority only apply to a non-streaming asset.
/// The bus list is the four buses of the engine plus those of the project's <c>.audioMixer</c> asset (plan P57), and the
/// Waveform row draws the main audio file, computed when the asset is loaded and when its file changes (plan P56).
/// </remarks>
public sealed class SoundAssetInspectorPanel : IDisposable
{
    private const float MaxDistanceRange = 100000f;
    private const float MaxRolloffRange = 10f;
    private const float MaxDopplerRange = 10f;

    /// <summary>Tag of the strip that draws the audio file, for finding it in the visual tree.</summary>
    internal const string WaveformEnvelopeTag = "sound-waveform";

    /// <summary>Tag of the line under the drawing (duration, sample rate and channels, or why there is no drawing).</summary>
    internal const string WaveformTextTag = "sound-waveform-text";

    /// <summary>The colour of the entry of a bus that neither the engine nor the project mixer asset has.</summary>
    private static readonly Microsoft.Xna.Framework.Color UnknownBusColor = Microsoft.Xna.Framework.Color.Orange;

    private readonly MGWindow _window;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly WaveformEnvelopeSource _waveformSource = new();

    private MGDockPanel _root;
    private MGTextBlock _headerText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGStackPanel _fieldStack;

    private SoundAsset _soundAsset;
    private string _loadedRelativePath;
    private bool _isDirty;
    private bool _suppressControlCallbacks;

    private MGComboBox<SoundBusChoice> _busCombo;
    private List<SoundBusChoice> _busChoices = new();

    private AudioWaveformResult _waveformResult;
    private MGTextBlock _waveformText;

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
        RecomputeWaveform();
        RefreshInspector();
    }

    /// <summary>
    /// Lists the buses again from the project's <c>.audioMixer</c> asset as it is on disk (plan P57), for instance after the asset
    /// was saved. The bus of the sound is kept selected and the asset is not touched. Does nothing when no sound is shown.
    /// </summary>
    public void RefreshBusChoices()
    {
        if (_busCombo == null || _soundAsset == null)
        {
            return;
        }

        bool wasSuppressed = _suppressControlCallbacks;
        _suppressControlCallbacks = true;
        try
        {
            SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset projectMixer);
            ApplyBusChoices(projectMixer);
        }
        finally
        {
            _suppressControlCallbacks = wasSuppressed;
        }
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
        _busCombo = null;
        _waveformText = null;

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
        _fieldStack.TryAddChild(CreateWaveformRow());
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
        _fieldStack.TryAddChild(CreateEnumRow(
            "Spatial", _soundAsset.SpatialMode, value => _soundAsset.SpatialMode = value));
        _fieldStack.TryAddChild(CreateEnumRow(
            "Distance model", _soundAsset.DistanceModel, value => _soundAsset.DistanceModel = value));
        _fieldStack.TryAddChild(CreateNumericRow(
            "Reference distance", MaxDistanceRange, 1f, _soundAsset.ReferenceDistance,
            value => _soundAsset.ReferenceDistance = MathF.Round(value, 2)));
        _fieldStack.TryAddChild(CreateNumericRow(
            "Max distance", MaxDistanceRange, 1f, _soundAsset.MaxDistance,
            value => _soundAsset.MaxDistance = MathF.Round(value, 2)));
        if (_soundAsset.MaxDistance == float.MaxValue)
        {
            // The field shows its own maximum: the asset keeps float.MaxValue until the user edits the field.
            _fieldStack.TryAddChild(CreateHelpText("Max distance: no limit."));
        }

        _fieldStack.TryAddChild(CreateNumericRow(
            "Rolloff", MaxRolloffRange, 0.1f, _soundAsset.RolloffFactor,
            value => _soundAsset.RolloffFactor = MathF.Round(value, 2)));
        _fieldStack.TryAddChild(CreateNumericRow(
            "Doppler factor", MaxDopplerRange, 0.1f, _soundAsset.DopplerFactor,
            value => _soundAsset.DopplerFactor = MathF.Round(value, 2)));
        _fieldStack.TryAddChild(CreateHelpText("Spatial settings apply when the sound is played at a position. Doppler 0 = off."));
        _fieldStack.TryAddChild(CreateHelpText(
            $"Parameter bindings: {_soundAsset.ParameterBindings.Count} (edit the .sound file)"));

        _suppressControlCallbacks = false;
    }

    private MGElement CreateAudioFileRow()
    {
        var row = CreateRow("Audio file");
        row.TryAddChild(CreateAudioFileSelector(
            _soundAsset.AudioFileAssetId,
            assetId =>
            {
                _soundAsset.AudioFileAssetId = assetId;
                RecomputeWaveform();
            }));
        return row;
    }

    /// <summary>
    /// The drawing of the main audio file and a line about it: duration, sample rate and channels, or why there is no drawing. It
    /// shows what <see cref="RecomputeWaveform"/> computed, and computes nothing itself.
    /// </summary>
    private MGElement CreateWaveformRow()
    {
        var row = CreateRow("Waveform");

        var envelope = new AudioEnvelopeControl(_window, _waveformSource)
        {
            ShowLevelMarks = false,
            Tag = WaveformEnvelopeTag,
        };
        _waveformText = new MGTextBlock(_window, DescribeWaveform(_waveformResult))
        {
            Opacity = 0.7f,
            Tag = WaveformTextTag,
        };

        var content = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 2 };
        content.TryAddChild(envelope);
        content.TryAddChild(_waveformText);
        row.TryAddChild(content);
        return row;
    }

    /// <summary>
    /// Decodes the main audio file once to draw it (plan P56), when the asset is loaded and when the file selector changes. The
    /// variation files are not drawn, and <c>is_streaming</c> makes no difference. Updates the Waveform row when there is one.
    /// </summary>
    private void RecomputeWaveform()
    {
        _waveformResult = _soundAsset == null
            ? null
            : AudioWaveformBuilder.BuildFromAsset(_soundAsset.AudioFileAssetId, AudioWaveformBuilder.DefaultColumnCount);

        _waveformSource.Waveform = _waveformResult?.Waveform;
        if (_waveformText != null)
        {
            _waveformText.Text = DescribeWaveform(_waveformResult);
        }
    }

    private static string DescribeWaveform(AudioWaveformResult result)
    {
        if (result == null)
        {
            return string.Empty;
        }

        AudioWaveform waveform = result.Waveform;
        if (waveform == null)
        {
            return result.Reason;
        }

        // Whole hundredths of a second first, so that 59.999 s reads 1:00.00 and not 0:60.00.
        long hundredths = (long)Math.Round(waveform.Duration.TotalSeconds * 100.0);
        string duration = hundredths < 6000
            ? string.Create(CultureInfo.InvariantCulture, $"{hundredths / 100.0:0.00} s")
            : string.Create(CultureInfo.InvariantCulture, $"{hundredths / 6000}:{hundredths % 6000 / 100.0:00.00}");
        string channels = waveform.ChannelCount == 1 ? "mono" : "stereo";
        return string.Create(CultureInfo.InvariantCulture, $"{duration}, {waveform.SampleRate} Hz, {channels}");
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

        var combo = new MGComboBox<SoundBusChoice>(_window)
        {
            MinWidth = 140,
        };
        combo.DropdownItemTemplate = choice =>
        {
            var button = combo.CreateDefaultDropdownButton();
            if (choice.IsUnknown)
            {
                button.SetContent(DescribeBusChoice(choice), UnknownBusColor);
            }
            else
            {
                button.SetContent(choice.Name);
            }

            return button;
        };
        combo.SelectedItemTemplate = choice => new MGTextBlock(
            _window,
            DescribeBusChoice(choice),
            choice.IsUnknown ? UnknownBusColor : null)
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _busCombo = combo;
        SoundBusChoices.TryLoadProjectMixer(out AudioMixerAsset projectMixer);
        ApplyBusChoices(projectMixer);

        combo.SelectedItemChanged += (_, args) =>
        {
            if (_suppressControlCallbacks || args.NewValue == null)
            {
                return;
            }

            ChooseBus(args.NewValue);
        };

        row.TryAddChild(combo);
        return row;
    }

    private static string DescribeBusChoice(SoundBusChoice choice)
    {
        return choice.IsUnknown ? $"{choice.Name} (unknown bus)" : choice.Name;
    }

    /// <summary>
    /// Lists the buses (plan P57) for the bus the sound has now and selects that one. The caller suppresses the control callbacks:
    /// nothing is written to the asset.
    /// </summary>
    private void ApplyBusChoices(AudioMixerAsset projectMixer)
    {
        if (_busCombo.IsDropdownOpen)
        {
            // The entries are about to be replaced under the open list.
            _busCombo.IsDropdownOpen = false;
        }

        _busChoices = SoundBusChoices.Resolve(projectMixer, _soundAsset.BusName);
        _busCombo.SetItemsSource(_busChoices);
        _busCombo.SelectedItem = FindBusChoice(_soundAsset.BusName);
    }

    /// <summary>The entry that stands for <paramref name="busName"/>, compared ignoring case like the mixer does, or null.</summary>
    private SoundBusChoice FindBusChoice(string busName)
    {
        for (var i = 0; i < _busChoices.Count; i++)
        {
            if (_busChoices[i].Name.Equals(busName, StringComparison.OrdinalIgnoreCase))
            {
                return _busChoices[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The user picked an entry. A real bus is written to the asset. The unknown entry is not a bus a sound can be given: the asset is
    /// left as it is and the list shows the bus the asset really has again.
    /// </summary>
    private void ChooseBus(SoundBusChoice choice)
    {
        if (choice.IsUnknown)
        {
            _suppressControlCallbacks = true;
            _busCombo.SelectedItem = FindBusChoice(_soundAsset.BusName);
            _suppressControlCallbacks = false;
            return;
        }

        _soundAsset.BusName = choice.Name;
        SetDirty(true);
    }

    private MGElement CreateNumericRow(string label, float max, float step, float value, Action<float> applyValue)
    {
        var row = CreateRow(label);
        row.TryAddChild(CreateNumericField(string.Empty, 0f, max, step, value, applyValue));
        return row;
    }

    private MGElement CreateEnumRow<T>(string label, T value, Action<T> applyValue) where T : struct, Enum
    {
        var row = CreateRow(label);

        var combo = new MGComboBox<T>(_window)
        {
            MinWidth = 140,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(item.ToString());
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, item.ToString())
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        combo.SetItemsSource(Enum.GetValues<T>());
        combo.SelectedItem = value;
        combo.SelectedItemChanged += (_, args) =>
        {
            if (_suppressControlCallbacks)
            {
                return;
            }

            applyValue(args.NewValue);
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

    /// <summary>
    /// The columns of the drawing of a file for <see cref="AudioEnvelopeControl"/>: a linear scale, the lowest sample below the axis
    /// and the highest above it (a column that does not cross zero is drawn from the axis), no inner band.
    /// </summary>
    private sealed class WaveformEnvelopeSource : IAudioEnvelopeSource
    {
        public AudioWaveform Waveform { get; set; }

        public int ColumnCount => Waveform?.ColumnCount ?? 0;

        public void GetColumn(int index, out float lower, out float upper, out float inner)
        {
            AudioWaveform waveform = Waveform;
            if (waveform == null || (uint)index >= (uint)waveform.ColumnCount)
            {
                lower = 0f;
                upper = 0f;
                inner = 0f;
                return;
            }

            lower = Math.Min(waveform.GetMinimum(index), 0f);
            upper = Math.Max(waveform.GetMaximum(index), 0f);
            inner = 0f;
        }
    }
}
