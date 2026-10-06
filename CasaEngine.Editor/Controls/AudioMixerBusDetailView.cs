#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The detail of the bus selected in the strips of the <see cref="AudioMixerPanel"/> (plan T10.7, decision P50): its insert effects in
/// order (up, down, delete, one numeric field per parameter, the filter type of a biquad, the source bus of a ducking, and "Add
/// effect") and its sends (a level per target, 0 removes the send, and a row to add one). Every edit goes through the
/// <see cref="AudioMixerDocument"/>: the view never changes the asset itself.
/// </summary>
/// <remarks>
/// <para>
/// One history entry per user intent. Up, down, delete, add and a change of combo are one entry each. A numeric field raises a change
/// for every keystroke that reads as a number and for every click on its +/- buttons, so its changes are held back as a burst: the
/// last value is written to the document once the burst is over, as one entry, 0.5 s after the last change (<see cref="Update"/>) or
/// when the field loses the keyboard focus, and at once when another edit, a save, a reload, the end of the document or a play
/// session starts. While a burst is open the asset and the live mixer are not touched yet.
/// </para>
/// <para>
/// The controls are rebuilt only when the structure changes (another bus, another list of effects or sends, another list of buses to
/// choose from) and only on a change of the document, never per frame; otherwise the values of the document are written into the
/// existing controls, which keeps the field that is being typed in. Writing a value into a control is never an edit
/// (<c>_isSyncing</c>). An operation the document refuses (a send that makes a cycle, a fifth effect, a ducking source that makes a
/// cycle) shows the message of the document through the status callback and the controls are brought back to the document on the next
/// <see cref="Update"/>. The controls that edit are disabled while <see cref="SetEditable"/> is false (a play session, P51).
/// </para>
/// <para>
/// The controls carry a <c>Tag</c> naming them: <c>detail-title</c>, <c>effect-up:0</c>, <c>effect-down:0</c>,
/// <c>effect-delete:0</c>, <c>effect-param:0:1</c> (effect, parameter), <c>effect-filter:0</c>, <c>effect-source:0</c>,
/// <c>effect-add-kind</c>, <c>effect-add</c>, <c>send-level:Reverb</c>, <c>send-new-target</c>, <c>send-new-level</c>.
/// </para>
/// </remarks>
internal sealed class AudioMixerBusDetailView : IDisposable
{
    internal const string PlayLockRefusalText = "Mixer edits are disabled during a play session.";

    /// <summary>How long a field must stay unchanged for its burst of changes to be written as one history entry.</summary>
    internal const float BurstIdleSeconds = 0.5f;

    private const string HintKey = "hint";
    private const char KeySeparator = '\u001f';
    private const int LabelWidth = 130;
    private const int SmallButtonWidth = 54;

    private enum BurstKind
    {
        None,
        EffectParameter,
        SendLevel,
    }

    private sealed class EffectRow
    {
        public EffectRow(AudioMixerEffectKind kind)
        {
            Kind = kind;
        }

        public AudioMixerEffectKind Kind { get; }

        public MGButton? Up { get; set; }

        public MGButton? Down { get; set; }

        public MGButton? Delete { get; set; }

        public List<NumericField> Fields { get; } = new();

        public MGComboBox<string>? FilterCombo { get; set; }

        public MGComboBox<string>? SourceCombo { get; set; }
    }

    private sealed class SendRow
    {
        public SendRow(string target, NumericField level)
        {
            Target = target;
            Level = level;
        }

        public string Target { get; }

        public NumericField Level { get; }
    }

    private readonly MGWindow _window;
    private readonly Func<AudioService?> _audioServiceProvider;
    private readonly Action<string> _reportStatus;
    private readonly MGStackPanel _root;
    private readonly List<EffectRow> _effectRows = new();
    private readonly List<SendRow> _sendRows = new();
    private readonly List<NumericField> _fields = new();
    private readonly List<string> _busNames = new();
    private readonly StringBuilder _keyBuilder = new();

    private AudioMixerDocument? _document;
    private string? _bus;
    private string? _builtKey;
    private bool _canEdit = true;
    private bool _isSyncing;
    private bool _resyncPending;
    private bool _isDisposed;
    private NumericField? _activeField;

    private MGComboBox<string>? _addKindCombo;
    private MGButton? _addButton;
    private AudioMixerEffectKind _addKind = AudioMixerEffectKind.Biquad;
    private MGComboBox<string>? _newSendTarget;
    private NumericField? _newSendLevel;
    private string? _newSendTargetName;

    private BurstKind _burstKind;
    private NumericField? _burstField;
    private int _burstEffectIndex;
    private int _burstParameter;
    private string? _burstSendTarget;
    private float _burstValue;
    private float _burstIdleSeconds;

    /// <param name="window">The window the controls belong to.</param>
    /// <param name="audioServiceProvider">Gives the audio service of the editor (its live buses are offered as targets), or null.</param>
    /// <param name="reportStatus">Shows a message in the status line of the panel.</param>
    public AudioMixerBusDetailView(MGWindow window, Func<AudioService?> audioServiceProvider, Action<string> reportStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _audioServiceProvider = audioServiceProvider ?? throw new ArgumentNullException(nameof(audioServiceProvider));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));

        _root = new MGStackPanel(window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        if (window.Desktop != null)
        {
            window.Desktop.FocusedKeyboardHandlerChanged += OnFocusedKeyboardHandlerChanged;
        }
    }

    /// <summary>The element to put in the panel.</summary>
    public MGElement Root => _root;

    /// <summary>The bus whose detail is shown, or null.</summary>
    public string? SelectedBus => _bus;

    /// <summary>True while a numeric field has changes the document does not know yet (a burst is open).</summary>
    public bool HasPendingEdit => _burstField != null;

    /// <summary>True while the view writes the last value of a burst to the document: the panel records that entry even in a play session.</summary>
    public bool IsCommittingBurst { get; private set; }

    /// <summary>The document the view edits, null when there is none. Whatever was shown is dropped.</summary>
    public void SetDocument(AudioMixerDocument? document)
    {
        DiscardBurst();
        _document = document;
        _builtKey = null;
        Refresh();
    }

    /// <summary>Shows the detail of <paramref name="bus"/>, or the hint when it is null. The burst of the previous bus is written first.</summary>
    public void Show(string? bus)
    {
        FlushPending();
        _bus = bus;
        Refresh();
    }

    /// <summary>
    /// Brings the controls in line with the document. Call it when the document changed, never per frame. A burst still open is dropped:
    /// the document changed under it.
    /// </summary>
    public void Refresh()
    {
        DiscardBurst();

        var document = _document;
        var busData = document != null && _bus != null ? FindBus(_bus) : null;
        if (document == null || busData == null)
        {
            ShowHint(document != null);
            return;
        }

        _bus = busData.Name;
        CollectBusNames();
        string key = BuildKey(busData);
        if (!string.Equals(key, _builtKey, StringComparison.Ordinal))
        {
            Rebuild(busData, key);
        }
        else
        {
            SyncValues(busData);
        }

        ApplyEnabledStates();
    }

    /// <summary>Enables or disables the controls that edit (they are disabled while the editor history is suspended, P51).</summary>
    public void SetEditable(bool canEdit)
    {
        if (_canEdit == canEdit)
        {
            return;
        }

        _canEdit = canEdit;
        ApplyEnabledStates();
    }

    /// <summary>
    /// Gives the view the frame: closes a burst of a field that stayed unchanged for <see cref="BurstIdleSeconds"/> and brings back the
    /// controls after a refused edit. Allocation free while nothing happens.
    /// </summary>
    public void Update(float elapsedSeconds)
    {
        if (_burstField != null)
        {
            _burstIdleSeconds += elapsedSeconds;
            if (_burstIdleSeconds >= BurstIdleSeconds)
            {
                FlushPending();
            }
        }

        // A burst still open is not dropped by a refresh: the controls are brought back once it is over.
        if (_resyncPending && _burstField == null)
        {
            _resyncPending = false;
            Refresh();
        }
    }

    /// <summary>Writes the burst of a numeric field, if one is open, to the document as one history entry.</summary>
    public void FlushPending()
    {
        if (_burstField == null)
        {
            return;
        }

        var kind = _burstKind;
        int effectIndex = _burstEffectIndex;
        int parameter = _burstParameter;
        string? sendTarget = _burstSendTarget;
        float value = _burstValue;
        DiscardBurst();

        var document = _document;
        string? bus = _bus;
        if (document == null || bus == null)
        {
            return;
        }

        bool wasCommitting = IsCommittingBurst;
        IsCommittingBurst = true;
        try
        {
            if (kind == BurstKind.EffectParameter)
            {
                CommitEffectParameter(document, bus, effectIndex, parameter, value);
            }
            else if (kind == BurstKind.SendLevel && sendTarget != null)
            {
                CommitSendLevel(document, bus, sendTarget, value);
            }
        }
        finally
        {
            IsCommittingBurst = wasCommitting;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DiscardBurst();

        if (_window.Desktop != null)
        {
            _window.Desktop.FocusedKeyboardHandlerChanged -= OnFocusedKeyboardHandlerChanged;
        }
    }

    // ------------------------------------------------------------------ build

    private void ShowHint(bool hasDocument)
    {
        _root.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;

        if (string.Equals(_builtKey, HintKey, StringComparison.Ordinal))
        {
            return;
        }

        _builtKey = HintKey;
        ClearRows();
        _root.TryAddChild(new MGTextBlock(_window, "Click the name of a bus in the strips to edit its effects and sends.")
        {
            Opacity = 0.7f,
            WrapText = true,
            Tag = "detail-hint",
        });
    }

    private void ClearRows()
    {
        _effectRows.Clear();
        _sendRows.Clear();
        _fields.Clear();
        _addKindCombo = null;
        _addButton = null;
        _newSendTarget = null;
        _newSendLevel = null;
        _root.TryRemoveAll();
    }

    /// <summary>The names the document accepts as a send target or a ducking source: the asset's buses, then the live mixer's, Master and Editor.</summary>
    private void CollectBusNames()
    {
        _busNames.Clear();

        var assetBuses = _document!.Asset.Buses;
        for (int index = 0; index < assetBuses.Count; index++)
        {
            string? name = assetBuses[index]?.Name;
            if (!string.IsNullOrWhiteSpace(name) && !ContainsName(_busNames, name))
            {
                _busNames.Add(name);
            }
        }

        var liveBuses = _audioServiceProvider()?.Mixer.Buses;
        if (liveBuses != null)
        {
            for (int index = 0; index < liveBuses.Count; index++)
            {
                if (!ContainsName(_busNames, liveBuses[index].Name))
                {
                    _busNames.Add(liveBuses[index].Name);
                }
            }
        }

        if (!ContainsName(_busNames, AudioBusNames.Master))
        {
            _busNames.Add(AudioBusNames.Master);
        }

        if (!ContainsName(_busNames, AudioBusNames.Editor))
        {
            _busNames.Add(AudioBusNames.Editor);
        }
    }

    private static bool ContainsName(List<string> names, string name)
    {
        for (int index = 0; index < names.Count; index++)
        {
            if (string.Equals(names[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What the controls are built for: compared as a whole to know whether they must be rebuilt.</summary>
    private string BuildKey(AudioMixerBusData bus)
    {
        var builder = _keyBuilder;
        builder.Clear();
        builder.Append(bus.Name).Append(KeySeparator);

        for (int index = 0; index < bus.Effects.Count; index++)
        {
            var data = bus.Effects[index];
            builder.Append((int)AudioMixerEffectCatalog.GetKind(data));
            if (data is AudioMixerDuckingEffectData ducking && !ContainsName(_busNames, ducking.Source))
            {
                // A source the choices do not hold is added to the list of its combo, so the combo is built for it.
                builder.Append('!').Append(ducking.Source);
            }

            builder.Append(KeySeparator);
        }

        builder.Append(KeySeparator);
        for (int index = 0; index < bus.Sends.Count; index++)
        {
            builder.Append(bus.Sends[index].Target).Append(KeySeparator);
        }

        builder.Append(KeySeparator);
        for (int index = 0; index < _busNames.Count; index++)
        {
            builder.Append(_busNames[index]).Append(KeySeparator);
        }

        return builder.ToString();
    }

    private void Rebuild(AudioMixerBusData bus, string key)
    {
        _builtKey = key;
        ClearRows();
        _root.Visibility = Visibility.Visible;

        string name = bus.Name;
        _root.TryAddChild(new MGTextBlock(_window, $"[b]Effects and sends of {AudioMixerPanel.EscapeMarkup(name)}[/b]")
        {
            Margin = new Thickness(0, 0, 0, 4),
            WrapText = true,
            Tag = "detail-title",
        });

        _root.TryAddChild(new MGTextBlock(_window, $"[b]Effects[/b] ({bus.Effects.Count}/{AudioBus.MaxEffects}), run in this order")
        {
            Opacity = 0.85f,
            Tag = "detail-effects-header",
        });

        for (int index = 0; index < bus.Effects.Count; index++)
        {
            _root.TryAddChild(BuildEffectBlock(name, index, bus.Effects[index]));
        }

        _root.TryAddChild(BuildAddEffectRow());

        _root.TryAddChild(new MGTextBlock(_window, $"[b]Sends[/b] ({bus.Sends.Count}/{AudioBus.MaxSends}), a level of 0 removes the send")
        {
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.85f,
            Tag = "detail-sends-header",
        });

        for (int index = 0; index < bus.Sends.Count; index++)
        {
            _root.TryAddChild(BuildSendRow(bus.Sends[index]));
        }

        _root.TryAddChild(BuildNewSendRow(bus));

        SyncValues(bus);
    }

    private MGStackPanel BuildEffectBlock(string bus, int index, AudioMixerEffectData data)
    {
        var kind = AudioMixerEffectCatalog.GetKind(data);
        var row = new EffectRow(kind);

        var block = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(0, 4, 0, 4),
        };

        var header = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 4 };
        header.TryAddChild(new MGTextBlock(_window, $"[b]{index + 1}. {AudioMixerEffectCatalog.GetDisplayName(kind)}[/b]")
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = LabelWidth + 30,
            WrapText = false,
        });

        row.Up = CreateButton("Up", $"effect-up:{index}", () => OnMoveEffect(index, index - 1), SmallButtonWidth);
        row.Down = CreateButton("Down", $"effect-down:{index}", () => OnMoveEffect(index, index + 1), SmallButtonWidth);
        row.Delete = CreateButton("Delete", $"effect-delete:{index}", () => OnDeleteEffect(index), SmallButtonWidth + 10);
        header.TryAddChild(row.Up);
        header.TryAddChild(row.Down);
        header.TryAddChild(row.Delete);
        block.TryAddChild(header);

        if (data is AudioMixerBiquadEffectData biquad)
        {
            var names = new List<string>();
            for (int type = 0; type < AudioMixerEffectCatalog.FilterTypes.Count; type++)
            {
                names.Add(AudioMixerEffectCatalog.GetDisplayName(AudioMixerEffectCatalog.FilterTypes[type]));
            }

            row.FilterCombo = CreateCombo(
                names,
                AudioMixerEffectCatalog.GetDisplayName(biquad.FilterType),
                $"effect-filter:{index}",
                selected => OnFilterTypeChanged(index, selected));
            block.TryAddChild(CreateLabeledRow("Filter type", row.FilterCombo));
        }
        else if (data is AudioMixerDuckingEffectData ducking)
        {
            var sources = new List<string>();
            for (int item = 0; item < _busNames.Count; item++)
            {
                if (!string.Equals(_busNames[item], bus, StringComparison.OrdinalIgnoreCase))
                {
                    sources.Add(_busNames[item]);
                }
            }

            if (!ContainsName(sources, ducking.Source))
            {
                sources.Add(ducking.Source);
            }

            row.SourceCombo = CreateCombo(sources, ducking.Source, $"effect-source:{index}", selected => OnDuckingSourceChanged(index, selected));
            block.TryAddChild(CreateLabeledRow("Source bus", row.SourceCombo));
        }

        var parameters = AudioMixerEffectCatalog.GetParameters(kind);
        for (int parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
        {
            var parameter = parameters[parameterIndex];
            int capturedParameter = parameterIndex;
            var field = CreateField(
                parameter.Min,
                parameter.Max,
                parameter.Step,
                AudioMixerEffectCatalog.GetValue(data, parameterIndex),
                $"effect-param:{index}:{parameterIndex}",
                (changed, value) => OnEffectParameterChanged(changed, index, capturedParameter, value));
            row.Fields.Add(field);
            block.TryAddChild(CreateLabeledRow(parameter.Name, field));
        }

        _effectRows.Add(row);
        return block;
    }

    private MGStackPanel BuildAddEffectRow()
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var names = new List<string>();
        for (int index = 0; index < AudioMixerEffectCatalog.Kinds.Count; index++)
        {
            names.Add(AudioMixerEffectCatalog.GetDisplayName(AudioMixerEffectCatalog.Kinds[index]));
        }

        row.TryAddChild(new MGTextBlock(_window, "New effect")
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = LabelWidth,
        });

        _addKindCombo = CreateCombo(names, AudioMixerEffectCatalog.GetDisplayName(_addKind), "effect-add-kind", OnAddKindChanged);
        row.TryAddChild(_addKindCombo);

        _addButton = CreateButton("Add effect", "effect-add", () => TryAddEffect(_addKind), 90);
        row.TryAddChild(_addButton);
        return row;
    }

    private MGStackPanel BuildSendRow(AudioMixerSendData send)
    {
        var field = CreateField(
            0f,
            1f,
            0.05f,
            send.Level,
            "send-level:" + send.Target,
            (changed, value) => OnSendLevelChanged(changed, send.Target, value));
        _sendRows.Add(new SendRow(send.Target, field));
        return CreateLabeledRow("To " + send.Target, field);
    }

    private MGStackPanel BuildNewSendRow(AudioMixerBusData bus)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var targets = new List<string>();
        for (int index = 0; index < _busNames.Count; index++)
        {
            string name = _busNames[index];
            if (!string.Equals(name, bus.Name, StringComparison.OrdinalIgnoreCase) && !HasSendTo(bus, name))
            {
                targets.Add(name);
            }
        }

        if (_newSendTargetName == null || !ContainsName(targets, _newSendTargetName))
        {
            _newSendTargetName = targets.Count > 0 ? targets[0] : null;
        }

        row.TryAddChild(new MGTextBlock(_window, "New send to")
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = LabelWidth,
        });

        _newSendTarget = CreateCombo(targets, _newSendTargetName, "send-new-target", OnNewSendTargetChanged);
        row.TryAddChild(_newSendTarget);

        _newSendLevel = CreateField(0f, 1f, 0.05f, 0f, "send-new-level", (changed, value) => OnNewSendLevelChanged(changed, value));
        row.TryAddChild(_newSendLevel);
        return row;
    }

    private static bool HasSendTo(AudioMixerBusData bus, string target)
    {
        for (int index = 0; index < bus.Sends.Count; index++)
        {
            if (string.Equals(bus.Sends[index].Target, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private MGStackPanel CreateLabeledRow(string label, MGElement control)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 6 };
        row.TryAddChild(new MGTextBlock(_window, AudioMixerPanel.EscapeMarkup(label))
        {
            VerticalAlignment = VerticalAlignment.Center,
            PreferredWidth = LabelWidth,
            WrapText = false,
        });
        row.TryAddChild(control);
        return row;
    }

    private NumericField CreateField(float min, float max, float step, float value, string tag, Action<NumericField, float> onChanged)
    {
        var field = new NumericField(_window, string.Empty, min, max, step)
        {
            Tag = tag,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Written before the handler exists: the initial value is not an edit.
        field.Value = value;
        field.ValueChanged += (_, newValue) =>
        {
            if (!_isSyncing)
            {
                onChanged(field, newValue);
            }
        };

        _fields.Add(field);
        return field;
    }

    private MGComboBox<string> CreateCombo(IReadOnlyList<string> items, string? selected, string tag, Action<string> onChanged)
    {
        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 140,
            Tag = tag,
        };
        combo.DropdownItemTemplate = item =>
        {
            var button = combo.CreateDefaultDropdownButton();
            button.SetContent(AudioMixerPanel.EscapeMarkup(item));
            return button;
        };
        combo.SelectedItemTemplate = item => new MGTextBlock(_window, AudioMixerPanel.EscapeMarkup(item))
        {
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        combo.SetItemsSource(new List<string>(items));
        if (selected != null)
        {
            combo.SelectedItem = selected;
        }

        combo.SelectedItemChanged += (_, e) =>
        {
            if (!_isSyncing && e.NewValue != null)
            {
                onChanged(e.NewValue);
            }
        };
        return combo;
    }

    private MGButton CreateButton(string label, string tag, Action onClick, int width)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = width,
            Tag = tag,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    // ------------------------------------------------------------------ values and enabled states

    /// <summary>Writes the values of the document into the controls that are already built, without any of them being an edit.</summary>
    private void SyncValues(AudioMixerBusData bus)
    {
        bool wasSyncing = _isSyncing;
        _isSyncing = true;
        try
        {
            for (int index = 0; index < _effectRows.Count && index < bus.Effects.Count; index++)
            {
                var row = _effectRows[index];
                var data = bus.Effects[index];

                for (int parameter = 0; parameter < row.Fields.Count; parameter++)
                {
                    SetFieldValue(row.Fields[parameter], AudioMixerEffectCatalog.GetValue(data, parameter));
                }

                if (row.FilterCombo != null)
                {
                    SetComboValue(row.FilterCombo, AudioMixerEffectCatalog.GetDisplayName(AudioMixerEffectCatalog.GetFilterType(data)));
                }

                if (row.SourceCombo != null)
                {
                    SetComboValue(row.SourceCombo, AudioMixerEffectCatalog.GetDuckingSource(data));
                }
            }

            for (int index = 0; index < _sendRows.Count && index < bus.Sends.Count; index++)
            {
                SetFieldValue(_sendRows[index].Level, bus.Sends[index].Level);
            }

            if (_newSendLevel != null)
            {
                SetFieldValue(_newSendLevel, 0f);
            }
        }
        finally
        {
            _isSyncing = wasSyncing;
        }
    }

    private void SetFieldValue(NumericField field, float value)
    {
        // The field a change is being processed for keeps what was typed in it.
        if (!ReferenceEquals(field, _activeField) && field.Value != value)
        {
            field.Value = value;
        }
    }

    private static void SetComboValue(MGComboBox<string> combo, string value)
    {
        if (!string.Equals(combo.SelectedItem, value, StringComparison.Ordinal))
        {
            combo.SelectedItem = value;
        }
    }

    private void ApplyEnabledStates()
    {
        var document = _document;
        bool canEdit = document != null && _canEdit;
        var bus = document != null && _bus != null ? FindBus(_bus) : null;
        int effectCount = bus?.Effects.Count ?? 0;
        int sendCount = bus?.Sends.Count ?? 0;

        for (int index = 0; index < _effectRows.Count; index++)
        {
            var row = _effectRows[index];
            row.Up!.IsEnabled = canEdit && index > 0;
            row.Down!.IsEnabled = canEdit && index < _effectRows.Count - 1;
            row.Delete!.IsEnabled = canEdit;

            for (int parameter = 0; parameter < row.Fields.Count; parameter++)
            {
                row.Fields[parameter].IsEnabled = canEdit;
            }

            if (row.FilterCombo != null)
            {
                row.FilterCombo.IsEnabled = canEdit;
            }

            if (row.SourceCombo != null)
            {
                row.SourceCombo.IsEnabled = canEdit;
            }
        }

        for (int index = 0; index < _sendRows.Count; index++)
        {
            _sendRows[index].Level.IsEnabled = canEdit;
        }

        if (_addKindCombo != null)
        {
            _addKindCombo.IsEnabled = canEdit;
            _addButton!.IsEnabled = canEdit && effectCount < AudioBus.MaxEffects;
        }

        if (_newSendTarget != null)
        {
            bool canAddSend = canEdit && sendCount < AudioBus.MaxSends && _newSendTargetName != null;
            _newSendTarget.IsEnabled = canAddSend;
            _newSendLevel!.IsEnabled = canAddSend;
        }
    }

    // ------------------------------------------------------------------ edits

    /// <summary>
    /// Appends a new effect of <paramref name="kind"/> with the defaults of the asset model, as one history entry. A ducking takes the
    /// first bus that can drive it (Voice when it can). A refusal (a fifth effect, no possible source) shows in the status line.
    /// </summary>
    internal bool TryAddEffect(AudioMixerEffectKind kind)
    {
        var document = _document;
        string? bus = _bus;
        if (document == null || bus == null)
        {
            return false;
        }

        FlushPending();
        if (IsRefusedByPlaySession())
        {
            return false;
        }

        string? source = null;
        if (kind == AudioMixerEffectKind.Ducking)
        {
            CollectBusNames();
            source = PickDuckingSource(document, bus);
            if (source == null)
            {
                _reportStatus($"No bus can be the ducking source of '{bus}'.");
                return false;
            }
        }

        if (!document.TryAddEffect(bus, AudioMixerEffectCatalog.CreateDefault(kind, source!), out string error))
        {
            _reportStatus(error);
            return false;
        }

        return true;
    }

    private string? PickDuckingSource(AudioMixerDocument document, string bus)
    {
        string? first = null;
        for (int index = 0; index < _busNames.Count; index++)
        {
            string name = _busNames[index];
            if (string.Equals(name, bus, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase)
                || AudioMixerAssetValidator.WouldMakeCycle(document.Asset, name, bus))
            {
                continue;
            }

            if (string.Equals(name, AudioBusNames.Voice, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            first ??= name;
        }

        return first;
    }

    private void OnAddKindChanged(string displayName)
    {
        for (int index = 0; index < AudioMixerEffectCatalog.Kinds.Count; index++)
        {
            if (string.Equals(AudioMixerEffectCatalog.GetDisplayName(AudioMixerEffectCatalog.Kinds[index]), displayName, StringComparison.Ordinal))
            {
                _addKind = AudioMixerEffectCatalog.Kinds[index];
                return;
            }
        }
    }

    private void OnMoveEffect(int index, int newIndex)
    {
        var document = _document;
        string? bus = _bus;
        if (document == null || bus == null)
        {
            return;
        }

        FlushPending();
        if (IsRefusedByPlaySession())
        {
            return;
        }

        if (!document.TryMoveEffect(bus, index, newIndex))
        {
            _reportStatus($"The effect {index + 1} of '{bus}' cannot move there.");
        }
    }

    private void OnDeleteEffect(int index)
    {
        var document = _document;
        string? bus = _bus;
        if (document == null || bus == null)
        {
            return;
        }

        FlushPending();
        if (IsRefusedByPlaySession())
        {
            return;
        }

        if (!document.TryRemoveEffect(bus, index))
        {
            _reportStatus($"The bus '{bus}' has no effect {index + 1}.");
        }
    }

    private void OnFilterTypeChanged(int index, string displayName)
    {
        for (int type = 0; type < AudioMixerEffectCatalog.FilterTypes.Count; type++)
        {
            var filterType = AudioMixerEffectCatalog.FilterTypes[type];
            if (string.Equals(AudioMixerEffectCatalog.GetDisplayName(filterType), displayName, StringComparison.Ordinal))
            {
                ReplaceEffect(index, data => AudioMixerEffectCatalog.WithFilterType(data, filterType));
                return;
            }
        }
    }

    private void OnDuckingSourceChanged(int index, string source)
    {
        // The live effect is replaced by the applier (the source of a ducking is fixed at construction): still one entry.
        ReplaceEffect(index, data => AudioMixerEffectCatalog.WithDuckingSource(data, source));
    }

    /// <summary>One edit of an effect that is not a numeric field: one history entry, or the message of the document.</summary>
    private void ReplaceEffect(int index, Func<AudioMixerEffectData, AudioMixerEffectData> change)
    {
        var document = _document;
        string? bus = _bus;
        var busData = bus != null ? FindBus(bus) : null;
        if (document == null || bus == null || busData == null)
        {
            return;
        }

        FlushPending();
        if (IsRefusedByPlaySession())
        {
            return;
        }

        if (index < 0 || index >= busData.Effects.Count)
        {
            _resyncPending = true;
            return;
        }

        var current = busData.Effects[index];
        var updated = change(current);
        if (updated == current)
        {
            return;
        }

        if (!document.TrySetEffect(bus, index, updated, out string error))
        {
            _reportStatus(error);
            _resyncPending = true;
        }
    }

    private void OnEffectParameterChanged(NumericField field, int effectIndex, int parameter, float value)
    {
        BeginOrExtendBurst(BurstKind.EffectParameter, field, effectIndex, parameter, null, value);
    }

    private void OnSendLevelChanged(NumericField field, string target, float value)
    {
        BeginOrExtendBurst(BurstKind.SendLevel, field, -1, -1, target, value);
    }

    private void OnNewSendLevelChanged(NumericField field, float value)
    {
        if (_newSendTargetName == null)
        {
            _resyncPending = true;
            return;
        }

        BeginOrExtendBurst(BurstKind.SendLevel, field, -1, -1, _newSendTargetName, value);
    }

    private void OnNewSendTargetChanged(string target)
    {
        // The level typed so far belongs to the previous target.
        FlushPending();
        _newSendTargetName = target;
    }

    /// <summary>
    /// A numeric field changed: opens the burst of that field, or extends it. A burst of another field is written first. Nothing is
    /// written to the document until the burst is over (see the remarks of the class).
    /// </summary>
    private void BeginOrExtendBurst(BurstKind kind, NumericField field, int effectIndex, int parameter, string? sendTarget, float value)
    {
        if (_document == null || _bus == null)
        {
            return;
        }

        if (EditorHistoryService.Current.IsSuspended)
        {
            // The controls are disabled during a play session; this is a change that raced the first frame of the session.
            _reportStatus(PlayLockRefusalText);
            _resyncPending = true;
            return;
        }

        _activeField = field;
        try
        {
            if (_burstField != null && !ReferenceEquals(_burstField, field))
            {
                FlushPending();
                if (!_fields.Contains(field))
                {
                    // The write of the previous burst rebuilt the controls: this one is gone.
                    return;
                }
            }

            _burstKind = kind;
            _burstField = field;
            _burstEffectIndex = effectIndex;
            _burstParameter = parameter;
            _burstSendTarget = sendTarget;
            _burstValue = value;
            _burstIdleSeconds = 0f;
        }
        finally
        {
            _activeField = null;
        }
    }

    private void DiscardBurst()
    {
        _burstKind = BurstKind.None;
        _burstField = null;
        _burstSendTarget = null;
        _burstIdleSeconds = 0f;
    }

    private void CommitEffectParameter(AudioMixerDocument document, string bus, int effectIndex, int parameter, float value)
    {
        var busData = FindBus(bus);
        if (busData == null || effectIndex < 0 || effectIndex >= busData.Effects.Count)
        {
            _resyncPending = true;
            return;
        }

        var current = busData.Effects[effectIndex];
        if (parameter < 0 || parameter >= AudioMixerEffectCatalog.GetParameters(AudioMixerEffectCatalog.GetKind(current)).Count)
        {
            _resyncPending = true;
            return;
        }

        var updated = AudioMixerEffectCatalog.WithValue(current, parameter, value);
        if (updated == current)
        {
            return;
        }

        if (!document.TrySetEffect(bus, effectIndex, updated, out string error))
        {
            _reportStatus(error);
            _resyncPending = true;
        }
    }

    private void CommitSendLevel(AudioMixerDocument document, string bus, string target, float level)
    {
        if (!document.TrySetSend(bus, target, level, out string error))
        {
            _reportStatus(error);
            _resyncPending = true;
        }
    }

    private bool IsRefusedByPlaySession()
    {
        if (!EditorHistoryService.Current.IsSuspended)
        {
            return false;
        }

        _reportStatus(PlayLockRefusalText);
        _resyncPending = true;
        return true;
    }

    private AudioMixerBusData? FindBus(string name)
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

    // ------------------------------------------------------------------ focus

    /// <summary>
    /// A field that loses the keyboard focus closes its burst, and shows its value again in the canonical form (what was typed may have
    /// been clamped, or may not be a number).
    /// </summary>
    private void OnFocusedKeyboardHandlerChanged(object? sender, EventArgs<MGElement> e)
    {
        if (_fields.Count == 0 || e.PreviousValue == null)
        {
            return;
        }

        var field = FindOwnField(e.PreviousValue);
        if (field == null || ReferenceEquals(field, FindOwnField(e.NewValue)))
        {
            return;
        }

        if (ReferenceEquals(field, _burstField))
        {
            FlushPending();
        }

        if (_fields.Contains(field))
        {
            bool wasSyncing = _isSyncing;
            _isSyncing = true;
            try
            {
                float current = field.Value;
                field.Value = current;
            }
            finally
            {
                _isSyncing = wasSyncing;
            }
        }
    }

    private NumericField? FindOwnField(MGElement? element)
    {
        for (var current = element; current != null; current = current.Parent)
        {
            if (current is NumericField field && _fields.Contains(field))
            {
                return field;
            }
        }

        return null;
    }
}
