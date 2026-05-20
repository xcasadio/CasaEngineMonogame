using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
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

            RefreshStatusText();
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

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        if (_particleAsset == null)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>
        {
            $"Name: {_particleAsset.Name}",
            $"Path: {_loadedRelativePath ?? "<none>"}",
            $"Dirty: {IsDirty}",
            $"Emitters: {_particleAsset.Emitters.Count}",
        };

        for (int emitterIndex = 0; emitterIndex < _particleAsset.Emitters.Count; emitterIndex++)
        {
            var emitter = _particleAsset.Emitters[emitterIndex];
            result.Add(string.Create(CultureInfo.InvariantCulture,
                $"Emitter[{emitterIndex}]: {emitter.Name}, enabled={emitter.Enabled}, duration={emitter.Duration:0.###}, looping={emitter.Looping}, max={emitter.MaxParticles}, rate={emitter.Emission.RateOverTime:0.###}, shape={emitter.Shape.ShapeType}, blend={emitter.Renderer.BlendMode}"));
        }

        return result;
    }

    public bool TryApplyAutomationPropertyOverrideAndSave(string propertyKey, string rawValue, out string statusMessage)
    {
        statusMessage = string.Empty;

        if (_particleAsset == null)
        {
            statusMessage = "No particle asset loaded.";
            return false;
        }

        if (!TryApplyAutomationProperty(propertyKey, rawValue, out statusMessage))
        {
            return false;
        }

        if (!TrySaveLoadedAsset(out string? saveError))
        {
            statusMessage = saveError ?? "Unable to save particle asset.";
            return false;
        }

        if (!ReloadFromDisk())
        {
            statusMessage = "Particle property was saved, but reload from disk failed.";
            return false;
        }

        statusMessage = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? $"Saved particle property '{propertyKey}'."
            : $"Saved {_loadedRelativePath} ({propertyKey}={rawValue})";
        return true;
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

        RefreshHeaderText();
        RefreshSourceText();
        RefreshStatusText();

        _emitterStack.TryAddChild(BuildSectionHeader("Asset"));
        _emitterStack.TryAddChild(BuildPropertyRow("Name", CreateTextBox(_particleAsset.Name, value => ApplyChange(() =>
        {
            _particleAsset.Name = string.IsNullOrWhiteSpace(value) ? "Particle Effect" : value;
            RefreshHeaderText();
        }, "Name"))));

        _emitterStack.TryAddChild(BuildSectionHeader($"Emitters ({_particleAsset.Emitters.Count})"));
        if (_particleAsset.Emitters.Count == 0)
        {
            _emitterStack.TryAddChild(BuildText("No emitters in asset."));
            return;
        }

        for (int emitterIndex = 0; emitterIndex < _particleAsset.Emitters.Count; emitterIndex++)
        {
            BuildEmitterEditor(_particleAsset.Emitters[emitterIndex], emitterIndex);
        }
    }

    private void BuildEmitterEditor(ParticleEmitterDefinition emitter, int emitterIndex)
    {
        if (_emitterStack == null)
        {
            return;
        }

        string sectionName = string.IsNullOrWhiteSpace(emitter.Name) ? $"Emitter {emitterIndex + 1}" : emitter.Name;
        _emitterStack.TryAddChild(BuildSectionHeader($"{emitterIndex + 1}. {sectionName}"));

        _emitterStack.TryAddChild(BuildPropertyRow("Emitter Name", CreateTextBox(emitter.Name, value => ApplyChange(() => emitter.Name = value, "Emitter Name"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Enabled", CreateCheckBox(emitter.Enabled, value => ApplyChange(() => emitter.Enabled = value, "Enabled"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Duration", CreateFloatField(emitter.Duration, 0.01f, 3600.0f, 0.1f, value => ApplyChange(() => emitter.Duration = value, "Duration"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Looping", CreateCheckBox(emitter.Looping, value => ApplyChange(() => emitter.Looping = value, "Looping"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Max Particles", CreateIntField(emitter.MaxParticles, 1, 100000, value => ApplyChange(() => emitter.MaxParticles = value, "Max Particles"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Rate", CreateFloatField(emitter.Emission.RateOverTime, 0.0f, 100000.0f, 1.0f, value => ApplyChange(() => emitter.Emission.RateOverTime = value, "Rate"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Shape", CreateEnumCombo(emitter.Shape.ShapeType, value => ApplyChange(() => emitter.Shape.ShapeType = value, "Shape"))));

        _emitterStack.TryAddChild(BuildPropertyRow("Lifetime Min", CreateFloatField(emitter.Initial.Lifetime.Min, 0.001f, 3600.0f, 0.1f, value => ApplyChange(() => emitter.Initial.Lifetime = new FloatRange(value, emitter.Initial.Lifetime.Max), "Lifetime Min"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Lifetime Max", CreateFloatField(emitter.Initial.Lifetime.Max, 0.001f, 3600.0f, 0.1f, value => ApplyChange(() => emitter.Initial.Lifetime = new FloatRange(emitter.Initial.Lifetime.Min, value), "Lifetime Max"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Speed Min", CreateFloatField(emitter.Initial.Speed.Min, 0.0f, 100000.0f, 0.1f, value => ApplyChange(() => emitter.Initial.Speed = new FloatRange(value, emitter.Initial.Speed.Max), "Speed Min"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Speed Max", CreateFloatField(emitter.Initial.Speed.Max, 0.0f, 100000.0f, 0.1f, value => ApplyChange(() => emitter.Initial.Speed = new FloatRange(emitter.Initial.Speed.Min, value), "Speed Max"))));

        _emitterStack.TryAddChild(BuildPropertyRow("Size Min", CreateVector2RangeEditor(emitter.Initial.Size.Min, value => ApplyChange(() => emitter.Initial.Size = new Vector2Range(value, emitter.Initial.Size.Max), "Size Min"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Size Max", CreateVector2RangeEditor(emitter.Initial.Size.Max, value => ApplyChange(() => emitter.Initial.Size = new Vector2Range(emitter.Initial.Size.Min, value), "Size Max"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Color", CreateColorEditor(GetGradientPreviewColor(emitter.Initial.StartColor), value => ApplyChange(() => emitter.Initial.StartColor = ColorGradient.Constant(value), "Color"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Texture", CreateTextureSelector(emitter)));
        _emitterStack.TryAddChild(BuildPropertyRow("Blend", CreateEnumCombo(emitter.Renderer.BlendMode, value => ApplyChange(() => emitter.Renderer.BlendMode = value, "Blend"))));
    }

    private MGElement BuildPropertyRow(string label, MGElement editor)
    {
        var row = new MGDockPanel(_window)
        {
            Margin = new Thickness(2, 2, 2, 4),
        };

        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(label))
        {
            PreferredWidth = 132,
            Margin = new Thickness(2, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        }, Dock.Left);

        row.TryAddChild(editor, Dock.Left);
        return row;
    }

    private MGElement CreateTextBox(string value, Action<string> onChanged)
    {
        var textBox = new MGTextBox(_window)
        {
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        textBox.SetText(value ?? string.Empty);
        textBox.TextChanged += (_, args) => onChanged(args.NewValue);
        return textBox;
    }

    private MGElement CreateCheckBox(bool value, Action<bool> onChanged)
    {
        var checkBox = new MGCheckBox(_window)
        {
            IsChecked = value,
        };
        checkBox.OnCheckStateChanged += (_, args) => onChanged(args.NewValue == true);
        return checkBox;
    }

    private MGElement CreateFloatField(float value, float min, float max, float step, Action<float> onChanged)
    {
        var field = new NumericField(_window, min: min, max: max, step: step)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.Value = value;
        field.ValueChanged += (_, nextValue) => onChanged(nextValue);
        return field;
    }

    private MGElement CreateIntField(int value, int min, int max, Action<int> onChanged)
    {
        var field = new NumericField(_window, min: min, max: max, step: 1.0f)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        field.Value = value;
        field.ValueChanged += (_, nextValue) => onChanged((int)MathF.Round(nextValue));
        return field;
    }

    private MGElement CreateVector2RangeEditor(Vector2 value, Action<Vector2> onChanged)
    {
        Vector2 currentValue = value;
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        row.TryAddChild(CreateLabeledFloatField("X", value.X, 0.0f, 100000.0f, 0.1f, nextValue =>
        {
            currentValue = new Vector2(nextValue, currentValue.Y);
            onChanged(currentValue);
        }));
        row.TryAddChild(CreateLabeledFloatField("Y", value.Y, 0.0f, 100000.0f, 0.1f, nextValue =>
        {
            currentValue = new Vector2(currentValue.X, nextValue);
            onChanged(currentValue);
        }));
        return row;
    }

    private MGElement CreateColorEditor(Color value, Action<Color> onChanged)
    {
        Color currentValue = value;
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };

        row.TryAddChild(CreateLabeledIntField("R", value.R, 0, 255, nextValue =>
        {
            currentValue = new Color((byte)nextValue, currentValue.G, currentValue.B, currentValue.A);
            onChanged(currentValue);
        }));
        row.TryAddChild(CreateLabeledIntField("G", value.G, 0, 255, nextValue =>
        {
            currentValue = new Color(currentValue.R, (byte)nextValue, currentValue.B, currentValue.A);
            onChanged(currentValue);
        }));
        row.TryAddChild(CreateLabeledIntField("B", value.B, 0, 255, nextValue =>
        {
            currentValue = new Color(currentValue.R, currentValue.G, (byte)nextValue, currentValue.A);
            onChanged(currentValue);
        }));
        row.TryAddChild(CreateLabeledIntField("A", value.A, 0, 255, nextValue =>
        {
            currentValue = new Color(currentValue.R, currentValue.G, currentValue.B, (byte)nextValue);
            onChanged(currentValue);
        }));
        return row;
    }

    private MGElement CreateLabeledFloatField(string label, float value, float min, float max, float step, Action<float> onChanged)
    {
        var field = new NumericField(_window, label, min, max, step)
        {
            PreferredWidth = 120,
        };
        field.Value = value;
        field.ValueChanged += (_, nextValue) => onChanged(nextValue);
        return field;
    }

    private MGElement CreateLabeledIntField(string label, int value, int min, int max, Action<int> onChanged)
    {
        var field = new NumericField(_window, label, min, max, 1.0f)
        {
            PreferredWidth = 120,
        };
        field.Value = value;
        field.ValueChanged += (_, nextValue) => onChanged((int)MathF.Round(nextValue));
        return field;
    }

    private MGElement CreateTextureSelector(ParticleEmitterDefinition emitter)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };

        var selector = new AssetSelector(_window)
        {
            AssetId = emitter.Renderer.TextureAssetId,
            Filter = IsTextureAsset,
        };
        selector.AssetChanged += (_, value) => ApplyChange(() => emitter.Renderer.TextureAssetId = value, "Texture");
        row.TryAddChild(selector);
        row.TryAddChild(CreateButton("Clear", () =>
        {
            selector.AssetId = Guid.Empty;
            ApplyChange(() => emitter.Renderer.TextureAssetId = Guid.Empty, "Texture");
        }));
        return row;
    }

    private MGElement CreateEnumCombo<TEnum>(TEnum selectedValue, Action<TEnum> onChanged)
        where TEnum : struct, Enum
    {
        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 160,
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

        var names = new List<string>(Enum.GetNames(typeof(TEnum)));
        combo.SetItemsSource(names);
        combo.SelectedItem = selectedValue.ToString();
        combo.SelectedItemChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.NewValue))
            {
                return;
            }

            if (Enum.TryParse(args.NewValue, out TEnum parsedValue))
            {
                onChanged(parsedValue);
            }
        };
        return combo;
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
            Margin = new Thickness(4, 8, 4, 2),
            Opacity = 0.85f,
            WrapText = true,
        };

    private MGTextBlock BuildText(string text)
        => new(_window, EscapeMarkup(text))
        {
            Margin = new Thickness(0, 0, 0, 2),
            Opacity = 0.8f,
            WrapText = true,
        };

    private void ApplyChange(Action change, string label)
    {
        if (_particleAsset == null)
        {
            return;
        }

        try
        {
            change();
            SetDirty(true);
            RefreshHeaderText();
            RefreshSourceText();
            RefreshStatusText();
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            SetStatus($"Failed to update {label}: {exception.Message}");
        }
    }

    private void RefreshHeaderText()
    {
        if (_headerText != null && _particleAsset != null)
        {
            _headerText.Text = $"[b]{EscapeMarkup(_particleAsset.Name)}[/b]";
        }
    }

    private void RefreshSourceText()
    {
        if (_sourceText != null && _particleAsset != null)
        {
            string id = _particleAsset.AssetId != Guid.Empty ? _particleAsset.AssetId.ToString() : _particleAsset.Id.ToString();
            string source = string.IsNullOrWhiteSpace(_loadedRelativePath) ? "<unsaved>" : _loadedRelativePath;
            _sourceText.Text = $"Source: {EscapeMarkup(source)}\nId: {id}  Version: {_particleAsset.Version}";
        }
    }

    private void RefreshStatusText()
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? string.Empty
            : IsDirty ? $"Modified {EscapeMarkup(_loadedRelativePath)}" : $"Asset: {EscapeMarkup(_loadedRelativePath)}";
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

    private bool TryApplyAutomationProperty(string propertyKey, string rawValue, out string statusMessage)
    {
        statusMessage = string.Empty;
        if (_particleAsset == null)
        {
            statusMessage = "No particle asset loaded.";
            return false;
        }

        if (!TryResolveAutomationPropertyTarget(propertyKey, out var emitterIndex, out var normalizedProperty, out statusMessage))
        {
            return false;
        }

        if (normalizedProperty == "name" && emitterIndex < 0)
        {
            ApplyChange(() => _particleAsset.Name = rawValue, "Name");
            return true;
        }

        if (emitterIndex < 0 || emitterIndex >= _particleAsset.Emitters.Count)
        {
            statusMessage = $"Emitter index {emitterIndex} is out of range.";
            return false;
        }

        var emitter = _particleAsset.Emitters[emitterIndex];
        switch (normalizedProperty)
        {
            case "name":
                ApplyChange(() => emitter.Name = rawValue, "Emitter Name");
                return true;
            case "enabled":
                return TryApplyBoolean(rawValue, value => emitter.Enabled = value, "Enabled", out statusMessage);
            case "duration":
                return TryApplyFloat(rawValue, 0.01f, value => emitter.Duration = value, "Duration", out statusMessage);
            case "looping":
                return TryApplyBoolean(rawValue, value => emitter.Looping = value, "Looping", out statusMessage);
            case "maxparticles":
                return TryApplyInt(rawValue, 1, value => emitter.MaxParticles = value, "Max Particles", out statusMessage);
            case "rate":
            case "rateovertime":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Emission.RateOverTime = value, "Rate", out statusMessage);
            case "shape":
                return TryApplyEnum<ParticleShapeType>(rawValue, value => emitter.Shape.ShapeType = value, "Shape", out statusMessage);
            case "lifetimemin":
                return TryApplyFloat(rawValue, 0.001f, value => emitter.Initial.Lifetime = new FloatRange(value, emitter.Initial.Lifetime.Max), "Lifetime Min", out statusMessage);
            case "lifetimemax":
                return TryApplyFloat(rawValue, 0.001f, value => emitter.Initial.Lifetime = new FloatRange(emitter.Initial.Lifetime.Min, value), "Lifetime Max", out statusMessage);
            case "speedmin":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Speed = new FloatRange(value, emitter.Initial.Speed.Max), "Speed Min", out statusMessage);
            case "speedmax":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Speed = new FloatRange(emitter.Initial.Speed.Min, value), "Speed Max", out statusMessage);
            case "sizeminx":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Size = new Vector2Range(new Vector2(value, emitter.Initial.Size.Min.Y), emitter.Initial.Size.Max), "Size Min X", out statusMessage);
            case "sizeminy":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Size = new Vector2Range(new Vector2(emitter.Initial.Size.Min.X, value), emitter.Initial.Size.Max), "Size Min Y", out statusMessage);
            case "sizemaxx":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Size = new Vector2Range(emitter.Initial.Size.Min, new Vector2(value, emitter.Initial.Size.Max.Y)), "Size Max X", out statusMessage);
            case "sizemaxy":
                return TryApplyFloat(rawValue, 0.0f, value => emitter.Initial.Size = new Vector2Range(emitter.Initial.Size.Min, new Vector2(emitter.Initial.Size.Max.X, value)), "Size Max Y", out statusMessage);
            case "blend":
                return TryApplyEnum<ParticleBlendMode>(rawValue, value => emitter.Renderer.BlendMode = value, "Blend", out statusMessage);
            case "texture":
                return TryApplyGuid(rawValue, value => emitter.Renderer.TextureAssetId = value, "Texture", out statusMessage);
            case "color":
            case "startcolor":
                return TryApplyColor(rawValue, value => emitter.Initial.StartColor = ColorGradient.Constant(value), "Color", out statusMessage);
            default:
                statusMessage = $"Unknown particle property '{propertyKey}'.";
                return false;
        }
    }

    private bool TryApplyBoolean(string rawValue, Action<bool> setter, string label, out string statusMessage)
    {
        if (!bool.TryParse(rawValue, out bool value))
        {
            statusMessage = $"Unable to parse boolean value '{rawValue}'.";
            return false;
        }

        ApplyChange(() => setter(value), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyFloat(string rawValue, float minimum, Action<float> setter, string label, out string statusMessage)
    {
        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            || float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum)
        {
            statusMessage = $"Unable to parse valid numeric value '{rawValue}' for {label}.";
            return false;
        }

        ApplyChange(() => setter(value), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyInt(string rawValue, int minimum, Action<int> setter, string label, out string statusMessage)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < minimum)
        {
            statusMessage = $"Unable to parse valid integer value '{rawValue}' for {label}.";
            return false;
        }

        ApplyChange(() => setter(value), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyEnum<TEnum>(string rawValue, Action<TEnum> setter, string label, out string statusMessage)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse(rawValue, ignoreCase: true, out TEnum value))
        {
            statusMessage = $"Unable to parse {label} value '{rawValue}'.";
            return false;
        }

        ApplyChange(() => setter(value), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyGuid(string rawValue, Action<Guid> setter, string label, out string statusMessage)
    {
        Guid value;
        if (string.Equals(rawValue, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawValue, "empty", StringComparison.OrdinalIgnoreCase))
        {
            value = Guid.Empty;
        }
        else if (!Guid.TryParse(rawValue, out value))
        {
            statusMessage = $"Unable to parse {label} asset id '{rawValue}'.";
            return false;
        }

        ApplyChange(() => setter(value), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyColor(string rawValue, Action<Color> setter, string label, out string statusMessage)
    {
        string[] components = rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (components.Length != 3 && components.Length != 4)
        {
            statusMessage = $"Color value '{rawValue}' must use R,G,B or R,G,B,A.";
            return false;
        }

        Span<byte> values = stackalloc byte[4];
        values[3] = 255;
        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            if (!byte.TryParse(components[componentIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[componentIndex]))
            {
                statusMessage = $"Color component '{components[componentIndex]}' must be between 0 and 255.";
                return false;
            }
        }

        byte red = values[0];
        byte green = values[1];
        byte blue = values[2];
        byte alpha = values[3];
        ApplyChange(() => setter(new Color(red, green, blue, alpha)), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryResolveAutomationPropertyTarget(string propertyKey, out int emitterIndex, out string normalizedProperty, out string statusMessage)
    {
        emitterIndex = 0;
        statusMessage = string.Empty;
        normalizedProperty = NormalizePropertyKey(propertyKey);
        string rawProperty = propertyKey.Trim();

        if (rawProperty.StartsWith("asset.", StringComparison.OrdinalIgnoreCase))
        {
            emitterIndex = -1;
            normalizedProperty = NormalizePropertyKey(rawProperty[6..]);
            return true;
        }

        if (!rawProperty.StartsWith("emitter", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int dotIndex = rawProperty.IndexOf('.');
        if (dotIndex <= "emitter".Length)
        {
            statusMessage = $"Particle property '{propertyKey}' must use emitterN.property syntax.";
            return false;
        }

        string indexText = rawProperty["emitter".Length..dotIndex];
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out emitterIndex))
        {
            statusMessage = $"Unable to parse emitter index '{indexText}'.";
            return false;
        }

        normalizedProperty = NormalizePropertyKey(rawProperty[(dotIndex + 1)..]);
        return true;
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

    private static Color GetGradientPreviewColor(ColorGradient gradient)
        => gradient.Evaluate(0.0f);

    private static bool IsTextureAsset(AssetInfo assetInfo)
    {
        string assetType = assetInfo.AssetType;
        return string.Equals(assetType, "texture", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "png", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "jpg", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "jpeg", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "bmp", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "tga", StringComparison.OrdinalIgnoreCase)
               || string.Equals(assetType, "dds", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePropertyKey(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '_' || character == '-' || char.IsWhiteSpace(character))
            {
                continue;
            }

            buffer[length] = char.ToLowerInvariant(character);
            length++;
        }

        return new string(buffer[..length]);
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}