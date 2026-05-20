using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Serialization;
using CasaEngine.Framework.Scene.World;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

public sealed class ParticleAssetInspectorPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly ParticlePreviewViewport? _particlePreview;

    private MGDockPanel? _root;
    private MGElement? _previewContent;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _emitterStack;

    private ParticleEffectAsset? _particleAsset;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private string? _savedSnapshot;
    private bool _isDirty;

    public ParticleAssetInspectorPanel(MGWindow window)
    {
        _window = window;
    }

    internal ParticleAssetInspectorPanel(MGWindow window, HostedEditorGameAdapter editorRuntime, GraphicsDevice graphicsDevice)
    {
        _window = window;
        _particlePreview = new ParticlePreviewViewport(window, graphicsDevice, editorRuntime);
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
        if (_particlePreview != null)
        {
            _root.TryAddChild(CreatePreviewContent(), Dock.Top);
        }

        _root.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _root;
    }

    public MGElement CreatePreviewContent()
    {
        if (_particlePreview != null)
        {
            return _particlePreview.CreateContent();
        }

        if (_previewContent != null)
        {
            return _previewContent;
        }

        _previewContent = new MGTextBlock(_window, "Particle preview unavailable.")
        {
            Margin = new Thickness(8, 6, 8, 4),
            Opacity = 0.75f,
            WrapText = true,
        };
        return _previewContent;
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
        _savedSnapshot = SerializeParticleAsset(particleAsset);
        _particlePreview?.LoadAsset(particleAsset, fullPath);
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
            _savedSnapshot = SerializeParticleAsset(_particleAsset);
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
            result.Add($"Emitter[{emitterIndex}] Curves: size={DescribeCurve(emitter.Simulation.SizeOverLifetime)}, alpha={DescribeCurve(emitter.Simulation.AlphaOverLifetime)}, velocity={DescribeCurve(emitter.Simulation.VelocityOverLifetime)}");
            result.Add($"Emitter[{emitterIndex}] Gradients: start={DescribeGradient(emitter.Initial.StartColor)}, color={DescribeGradient(emitter.Simulation.ColorOverLifetime)}");
        }

        var previewStates = _particlePreview?.GetAutomationStateSnapshot() ?? Array.Empty<string>();
        for (int stateIndex = 0; stateIndex < previewStates.Count; stateIndex++)
        {
            result.Add($"Preview {previewStates[stateIndex]}");
        }

        return result;
    }

    public World? GetOrCreatePreviewWorld()
    {
        return _particlePreview?.GetOrCreatePreviewWorld();
    }

    public void Update(GameTime gameTime)
    {
        _particlePreview?.Update(gameTime);
    }

    public void RefreshPreviewAfterDraw()
    {
        _particlePreview?.RefreshPreviewAfterDraw();
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
        _particlePreview?.Dispose();
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
        _emitterStack.TryAddChild(BuildPropertyRow("Emitters", CreateButton("Add Emitter", AddEmitter)));
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
        _emitterStack.TryAddChild(BuildPropertyRow("Emitter", CreateButton("Remove", () => RemoveEmitter(emitterIndex))));

        _emitterStack.TryAddChild(BuildPropertyRow("Emitter Name", CreateTextBox(emitter.Name, value => ApplyChange(() => emitter.Name = value, "Emitter Name"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Enabled", CreateCheckBox(emitter.Enabled, value => ApplyChange(() => emitter.Enabled = value, "Enabled"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Duration", CreateFloatField(emitter.Duration, 0.01f, 3600.0f, 0.1f, value => ApplyChange(() => emitter.Duration = value, "Duration"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Looping", CreateCheckBox(emitter.Looping, value => ApplyChange(() => emitter.Looping = value, "Looping"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Max Particles", CreateIntField(emitter.MaxParticles, 1, 100000, value => ApplyChange(() => emitter.MaxParticles = value, "Max Particles"))));
        _emitterStack.TryAddChild(BuildPropertyRow("Rate", CreateFloatField(emitter.Emission.RateOverTime, 0.0f, 100000.0f, 1.0f, value => ApplyChange(() => emitter.Emission.RateOverTime = value, "Rate"))));
        BuildBurstEditor(emitter, emitterIndex);
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
        _emitterStack.TryAddChild(BuildPropertyRow("Start Gradient", CreateGradientEditor(emitter.Initial.StartColor, (value, refresh) => ApplyChange(() => emitter.Initial.StartColor = value, "Start Gradient", refresh))));
        _emitterStack.TryAddChild(BuildPropertyRow("Size Curve", CreateCurveEditor(emitter.Simulation.SizeOverLifetime, (value, refresh) => ApplyChange(() => emitter.Simulation.SizeOverLifetime = value, "Size Curve", refresh))));
        _emitterStack.TryAddChild(BuildPropertyRow("Alpha Curve", CreateCurveEditor(emitter.Simulation.AlphaOverLifetime, (value, refresh) => ApplyChange(() => emitter.Simulation.AlphaOverLifetime = value, "Alpha Curve", refresh))));
        _emitterStack.TryAddChild(BuildPropertyRow("Velocity Curve", CreateCurveEditor(emitter.Simulation.VelocityOverLifetime, (value, refresh) => ApplyChange(() => emitter.Simulation.VelocityOverLifetime = value, "Velocity Curve", refresh))));
        _emitterStack.TryAddChild(BuildPropertyRow("Color Lifetime", CreateGradientEditor(emitter.Simulation.ColorOverLifetime, (value, refresh) => ApplyChange(() => emitter.Simulation.ColorOverLifetime = value, "Color Lifetime", refresh))));
    }

    private void BuildBurstEditor(ParticleEmitterDefinition emitter, int emitterIndex)
    {
        if (_emitterStack == null)
        {
            return;
        }

        _emitterStack.TryAddChild(BuildPropertyRow("Bursts", CreateButton("Add Burst", () => AddBurst(emitterIndex))));
        for (int burstIndex = 0; burstIndex < emitter.Emission.Bursts.Count; burstIndex++)
        {
            int capturedBurstIndex = burstIndex;
            ParticleBurst burst = emitter.Emission.Bursts[burstIndex];
            string label = $"Burst {burstIndex + 1}";
            var row = new MGStackPanel(_window, Orientation.Horizontal)
            {
                Spacing = 4,
            };

            row.TryAddChild(CreateLabeledFloatField("T", burst.Time, 0.0f, MathF.Max(0.0f, emitter.Duration), 0.05f, value => ApplyChange(() => burst.Time = value, $"{label} Time")));
            row.TryAddChild(CreateLabeledIntField("Min", burst.CountMin, 0, 100000, value => ApplyChange(() => burst.CountMin = value, $"{label} Min")));
            row.TryAddChild(CreateLabeledIntField("Max", burst.CountMax, 0, 100000, value => ApplyChange(() => burst.CountMax = value, $"{label} Max")));
            row.TryAddChild(CreateCompactButton("Remove", () => RemoveBurst(emitterIndex, capturedBurstIndex)));
            _emitterStack.TryAddChild(BuildPropertyRow(label, row));
        }
    }

    private void AddEmitter()
    {
        if (_particleAsset == null)
        {
            return;
        }

        int nextIndex = _particleAsset.Emitters.Count + 1;
        ApplyChange(
            () => _particleAsset.Emitters.Add(new ParticleEmitterDefinition { Name = $"Emitter {nextIndex}" }),
            "Add Emitter",
            refreshInspector: true);
    }

    private void RemoveEmitter(int emitterIndex)
    {
        ApplyChange(
            () =>
            {
                if (_particleAsset == null || emitterIndex < 0 || emitterIndex >= _particleAsset.Emitters.Count)
                {
                    return;
                }

                _particleAsset.Emitters.RemoveAt(emitterIndex);
            },
            "Remove Emitter",
            refreshInspector: true);
    }

    private void AddBurst(int emitterIndex)
    {
        ApplyChange(
            () =>
            {
                if (_particleAsset == null || emitterIndex < 0 || emitterIndex >= _particleAsset.Emitters.Count)
                {
                    return;
                }

                _particleAsset.Emitters[emitterIndex].Emission.Bursts.Add(new ParticleBurst());
            },
            "Add Burst",
            refreshInspector: true);
    }

    private void RemoveBurst(int emitterIndex, int burstIndex)
    {
        ApplyChange(
            () =>
            {
                if (_particleAsset == null || emitterIndex < 0 || emitterIndex >= _particleAsset.Emitters.Count)
                {
                    return;
                }

                var bursts = _particleAsset.Emitters[emitterIndex].Emission.Bursts;
                if (burstIndex < 0 || burstIndex >= bursts.Count)
                {
                    return;
                }

                bursts.RemoveAt(burstIndex);
            },
            "Remove Burst",
            refreshInspector: true);
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

    private MGElement CreateCurveEditor(FloatCurve curve, Action<FloatCurve, bool> onChanged)
    {
        FloatCurve currentCurve = CloneCurve(curve);
        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
        };

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        toolbar.TryAddChild(CreateCompactButton("Add Key", () =>
        {
            currentCurve = AddCurveKey(currentCurve);
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Constant", () =>
        {
            currentCurve = FloatCurve.Constant(1.0f);
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Fade In", () =>
        {
            currentCurve = FloatCurve.FadeIn();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Fade Out", () =>
        {
            currentCurve = FloatCurve.FadeOut();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Bell", () =>
        {
            currentCurve = FloatCurve.Bell();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Pulse", () =>
        {
            currentCurve = FloatCurve.Pulse();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Ease Out", () =>
        {
            currentCurve = FloatCurve.EaseOut();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Pop", () =>
        {
            currentCurve = FloatCurve.Pop();
            onChanged(currentCurve, true);
        }));
        toolbar.TryAddChild(CreateCompactButton("Reset", () =>
        {
            currentCurve = FloatCurve.Constant(1.0f);
            onChanged(currentCurve, true);
        }));
        stack.TryAddChild(toolbar);

        for (int keyIndex = 0; keyIndex < curve.Keys.Count; keyIndex++)
        {
            int capturedIndex = keyIndex;
            FloatCurveKey key = curve.Keys[keyIndex];
            var row = new MGStackPanel(_window, Orientation.Horizontal)
            {
                Spacing = 4,
            };
            row.TryAddChild(new MGTextBlock(_window, $"#{keyIndex + 1}")
            {
                PreferredWidth = 28,
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.TryAddChild(CreateLabeledFloatField("T", key.Time, 0.0f, 1.0f, 0.05f, value =>
            {
                FloatCurveKey currentKey = GetCurveKeyOrFallback(currentCurve, capturedIndex, key);
                currentCurve = ReplaceCurveKey(currentCurve, capturedIndex, value, currentKey.Value);
                onChanged(currentCurve, true);
            }));
            row.TryAddChild(CreateLabeledFloatField("V", key.Value, -100000.0f, 100000.0f, 0.05f, value =>
            {
                FloatCurveKey currentKey = GetCurveKeyOrFallback(currentCurve, capturedIndex, key);
                currentCurve = ReplaceCurveKey(currentCurve, capturedIndex, currentKey.Time, value);
                onChanged(currentCurve, false);
            }));
            row.TryAddChild(CreateCompactButton("Remove", () =>
            {
                currentCurve = RemoveCurveKey(currentCurve, capturedIndex);
                onChanged(currentCurve, true);
            }));
            stack.TryAddChild(row);
        }

        return stack;
    }

    private MGElement CreateGradientEditor(ColorGradient gradient, Action<ColorGradient, bool> onChanged)
    {
        ColorGradient currentGradient = CloneGradient(gradient);
        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 4,
        };

        var presetRow = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        presetRow.TryAddChild(CreateCompactButton("White", () =>
        {
            currentGradient = ColorGradient.White;
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Fire", () =>
        {
            currentGradient = ColorGradient.Fire();
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Smoke", () =>
        {
            currentGradient = ColorGradient.Smoke();
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Magic", () =>
        {
            currentGradient = ColorGradient.MagicBlue();
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Spark", () =>
        {
            currentGradient = ColorGradient.Spark();
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Ember", () =>
        {
            currentGradient = ColorGradient.Ember();
            onChanged(currentGradient, true);
        }));
        presetRow.TryAddChild(CreateCompactButton("Reset", () =>
        {
            currentGradient = ColorGradient.White;
            onChanged(currentGradient, true);
        }));
        stack.TryAddChild(presetRow);

        var colorToolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        colorToolbar.TryAddChild(new MGTextBlock(_window, "Color Keys")
        {
            PreferredWidth = 84,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8f,
        });
        colorToolbar.TryAddChild(CreateCompactButton("Add Color", () =>
        {
            currentGradient = AddGradientColorKey(currentGradient);
            onChanged(currentGradient, true);
        }));
        stack.TryAddChild(colorToolbar);

        for (int keyIndex = 0; keyIndex < gradient.ColorKeys.Count; keyIndex++)
        {
            int capturedIndex = keyIndex;
            ColorGradientKey key = gradient.ColorKeys[keyIndex];
            var row = new MGStackPanel(_window, Orientation.Horizontal)
            {
                Spacing = 4,
            };
            row.TryAddChild(new MGTextBlock(_window, $"#{keyIndex + 1}")
            {
                PreferredWidth = 28,
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.TryAddChild(CreateLabeledFloatField("T", key.Time, 0.0f, 1.0f, 0.05f, value =>
            {
                ColorGradientKey currentKey = GetColorKeyOrFallback(currentGradient, capturedIndex, key);
                currentGradient = ReplaceGradientColorKey(currentGradient, capturedIndex, value, currentKey.Color);
                onChanged(currentGradient, true);
            }));
            row.TryAddChild(CreateLabeledIntField("R", key.Color.R, 0, 255, value =>
            {
                ColorGradientKey currentKey = GetColorKeyOrFallback(currentGradient, capturedIndex, key);
                Color color = new((byte)value, currentKey.Color.G, currentKey.Color.B, currentKey.Color.A);
                currentGradient = ReplaceGradientColorKey(currentGradient, capturedIndex, currentKey.Time, color);
                onChanged(currentGradient, false);
            }));
            row.TryAddChild(CreateLabeledIntField("G", key.Color.G, 0, 255, value =>
            {
                ColorGradientKey currentKey = GetColorKeyOrFallback(currentGradient, capturedIndex, key);
                Color color = new(currentKey.Color.R, (byte)value, currentKey.Color.B, currentKey.Color.A);
                currentGradient = ReplaceGradientColorKey(currentGradient, capturedIndex, currentKey.Time, color);
                onChanged(currentGradient, false);
            }));
            row.TryAddChild(CreateLabeledIntField("B", key.Color.B, 0, 255, value =>
            {
                ColorGradientKey currentKey = GetColorKeyOrFallback(currentGradient, capturedIndex, key);
                Color color = new(currentKey.Color.R, currentKey.Color.G, (byte)value, currentKey.Color.A);
                currentGradient = ReplaceGradientColorKey(currentGradient, capturedIndex, currentKey.Time, color);
                onChanged(currentGradient, false);
            }));
            row.TryAddChild(CreateCompactButton("Remove", () =>
            {
                currentGradient = RemoveGradientColorKey(currentGradient, capturedIndex);
                onChanged(currentGradient, true);
            }));
            stack.TryAddChild(row);
        }

        var alphaToolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        alphaToolbar.TryAddChild(new MGTextBlock(_window, "Alpha Keys")
        {
            PreferredWidth = 84,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8f,
        });
        alphaToolbar.TryAddChild(CreateCompactButton("Add Alpha", () =>
        {
            currentGradient = AddGradientAlphaKey(currentGradient);
            onChanged(currentGradient, true);
        }));
        stack.TryAddChild(alphaToolbar);

        for (int keyIndex = 0; keyIndex < gradient.AlphaKeys.Count; keyIndex++)
        {
            int capturedIndex = keyIndex;
            AlphaGradientKey key = gradient.AlphaKeys[keyIndex];
            var row = new MGStackPanel(_window, Orientation.Horizontal)
            {
                Spacing = 4,
            };
            row.TryAddChild(new MGTextBlock(_window, $"#{keyIndex + 1}")
            {
                PreferredWidth = 28,
                VerticalAlignment = VerticalAlignment.Center,
            });
            row.TryAddChild(CreateLabeledFloatField("T", key.Time, 0.0f, 1.0f, 0.05f, value =>
            {
                AlphaGradientKey currentKey = GetAlphaKeyOrFallback(currentGradient, capturedIndex, key);
                currentGradient = ReplaceGradientAlphaKey(currentGradient, capturedIndex, value, currentKey.Alpha);
                onChanged(currentGradient, true);
            }));
            row.TryAddChild(CreateLabeledFloatField("A", key.Alpha, 0.0f, 1.0f, 0.05f, value =>
            {
                AlphaGradientKey currentKey = GetAlphaKeyOrFallback(currentGradient, capturedIndex, key);
                currentGradient = ReplaceGradientAlphaKey(currentGradient, capturedIndex, currentKey.Time, value);
                onChanged(currentGradient, false);
            }));
            row.TryAddChild(CreateCompactButton("Remove", () =>
            {
                currentGradient = RemoveGradientAlphaKey(currentGradient, capturedIndex);
                onChanged(currentGradient, true);
            }));
            stack.TryAddChild(row);
        }

        return stack;
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

    private MGButton CreateCompactButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 72,
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

    private void ApplyChange(Action change, string label, bool refreshInspector = false)
    {
        if (_particleAsset == null)
        {
            return;
        }

        try
        {
            string beforeSnapshot = SerializeParticleAsset(_particleAsset);
            change();

            string afterSnapshot = SerializeParticleAsset(_particleAsset);
            if (string.Equals(beforeSnapshot, afterSnapshot, StringComparison.Ordinal))
            {
                return;
            }

            if (TryGetHistoryContext(out var historyContext))
            {
                bool commandHasExecuted = false;
                EditorHistoryService.Current.Execute(
                    historyContext,
                    new EditorDelegateCommand(
                        BuildParticleCommandDescription(label),
                        () =>
                        {
                            if (!commandHasExecuted)
                            {
                                commandHasExecuted = true;
                                RefreshChangedParticleState(refreshInspector);
                                return;
                            }

                            ApplySerializedParticleAssetState(afterSnapshot, refreshInspector: true);
                        },
                        () => ApplySerializedParticleAssetState(beforeSnapshot, refreshInspector: true)));
                return;
            }

            RefreshChangedParticleState(refreshInspector);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            SetStatus($"Failed to update {label}: {exception.Message}");
        }
    }

    private void RefreshChangedParticleState(bool refreshInspector)
    {
        UpdateDirtyStateFromCurrentParticle();
        RefreshHeaderText();
        RefreshSourceText();
        RefreshStatusText();
        _particlePreview?.RefreshParticleAsset();
        if (refreshInspector)
        {
            RefreshInspector();
        }
    }

    private void ApplySerializedParticleAssetState(string snapshot, bool refreshInspector)
    {
        if (!TryCreateParticleAssetFromSnapshot(snapshot, out var particleAsset))
        {
            SetStatus("Unable to apply particle history state.");
            return;
        }

        ApplyLoadedAssetMetadata(particleAsset);
        _particleAsset = particleAsset;
        UpdateDirtyStateFromCurrentParticle();
        RefreshHeaderText();
        RefreshSourceText();
        RefreshStatusText();

        string? fullPath = GetLoadedFullPath();
        if (_particlePreview != null && fullPath != null)
        {
            _particlePreview.LoadAsset(particleAsset, fullPath);
        }
        else
        {
            _particlePreview?.RefreshParticleAsset();
        }

        if (refreshInspector)
        {
            RefreshInspector();
        }
    }

    private bool TryCreateParticleAssetFromSnapshot(string snapshot, out ParticleEffectAsset particleAsset)
    {
        particleAsset = new ParticleEffectAsset();
        try
        {
            particleAsset.Load(JObject.Parse(snapshot));
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return false;
        }
    }

    private void ApplyLoadedAssetMetadata(ParticleEffectAsset particleAsset)
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            particleAsset.AssetId = particleAsset.Id;
            return;
        }

        particleAsset.FileName = _loadedRelativePath;
        var assetInfo = AssetCatalog.GetByFileName(_loadedRelativePath)
                        ?? AssetCatalog.GetByFileName(_loadedRelativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        particleAsset.AssetId = assetInfo?.Id ?? particleAsset.Id;
        if (assetInfo != null)
        {
            particleAsset.FileName = assetInfo.FileName;
        }
    }

    private string? GetLoadedFullPath()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return null;
        }

        return Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
    }

    private void UpdateDirtyStateFromCurrentParticle()
    {
        if (_particleAsset == null)
        {
            SetDirty(false);
            return;
        }

        SetDirty(!string.Equals(SerializeParticleAsset(_particleAsset), _savedSnapshot, StringComparison.Ordinal));
    }

    private static string BuildParticleCommandDescription(string label)
        => label.StartsWith("Add ", StringComparison.OrdinalIgnoreCase)
           || label.StartsWith("Remove ", StringComparison.OrdinalIgnoreCase)
            ? label
            : $"Set {label}";

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
            case "startgradient":
            case "startcolorgradient":
            case "startcolorpreset":
                return TryApplyGradientPreset(rawValue, value => emitter.Initial.StartColor = value, "Start Gradient", out statusMessage);
            case "sizecurve":
            case "sizeoverlifetime":
            case "sizecurvepreset":
                return TryApplyFloatCurvePreset(rawValue, value => emitter.Simulation.SizeOverLifetime = value, "Size Curve", out statusMessage);
            case "alphacurve":
            case "alphaoverlifetime":
            case "alphacurvepreset":
                return TryApplyFloatCurvePreset(rawValue, value => emitter.Simulation.AlphaOverLifetime = value, "Alpha Curve", out statusMessage);
            case "velocitycurve":
            case "velocityoverlifetime":
            case "velocitycurvepreset":
                return TryApplyFloatCurvePreset(rawValue, value => emitter.Simulation.VelocityOverLifetime = value, "Velocity Curve", out statusMessage);
            case "colorlifetime":
            case "coloroverlifetime":
            case "colorgradient":
            case "colorgradientpreset":
                return TryApplyGradientPreset(rawValue, value => emitter.Simulation.ColorOverLifetime = value, "Color Lifetime", out statusMessage);
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
        if (!TryParseColor(rawValue, out Color color, out statusMessage))
        {
            return false;
        }

        ApplyChange(() => setter(color), label);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyFloatCurvePreset(string rawValue, Action<FloatCurve> setter, string label, out string statusMessage)
    {
        if (!TryCreateFloatCurve(rawValue, out FloatCurve curve, out statusMessage))
        {
            return false;
        }

        ApplyChange(() => setter(curve), label, refreshInspector: true);
        statusMessage = string.Empty;
        return true;
    }

    private bool TryApplyGradientPreset(string rawValue, Action<ColorGradient> setter, string label, out string statusMessage)
    {
        if (!TryCreateGradient(rawValue, out ColorGradient gradient, out statusMessage))
        {
            return false;
        }

        ApplyChange(() => setter(gradient), label, refreshInspector: true);
        statusMessage = string.Empty;
        return true;
    }

    private static bool TryParseColor(string rawValue, out Color color, out string statusMessage)
    {
        color = Color.White;
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
        color = new Color(red, green, blue, alpha);
        statusMessage = string.Empty;
        return true;
    }

    private static bool TryCreateFloatCurve(string rawValue, out FloatCurve curve, out string statusMessage)
    {
        curve = FloatCurve.Constant(1.0f);
        statusMessage = string.Empty;

        string trimmedValue = rawValue.Trim();
        if (trimmedValue.StartsWith("constant", StringComparison.OrdinalIgnoreCase))
        {
            float constantValue = 1.0f;
            int separatorIndex = trimmedValue.IndexOfAny(new[] { ':', '=' });
            if (separatorIndex >= 0)
            {
                string valueText = trimmedValue[(separatorIndex + 1)..].Trim();
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out constantValue)
                    || float.IsNaN(constantValue)
                    || float.IsInfinity(constantValue))
                {
                    statusMessage = $"Unable to parse curve constant value '{valueText}'.";
                    return false;
                }
            }

            curve = FloatCurve.Constant(constantValue);
            return true;
        }

        switch (NormalizePropertyKey(trimmedValue))
        {
            case "reset":
            case "one":
                curve = FloatCurve.Constant(1.0f);
                return true;
            case "zero":
                curve = FloatCurve.Constant(0.0f);
                return true;
            case "fadein":
                curve = FloatCurve.FadeIn();
                return true;
            case "fadeout":
                curve = FloatCurve.FadeOut();
                return true;
            case "bell":
                curve = FloatCurve.Bell();
                return true;
            case "pulse":
                curve = FloatCurve.Pulse();
                return true;
            case "easeout":
                curve = FloatCurve.EaseOut();
                return true;
            case "pop":
                curve = FloatCurve.Pop();
                return true;
            default:
                statusMessage = $"Unknown curve preset '{rawValue}'.";
                return false;
        }
    }

    private static bool TryCreateGradient(string rawValue, out ColorGradient gradient, out string statusMessage)
    {
        gradient = ColorGradient.White;
        statusMessage = string.Empty;

        string trimmedValue = rawValue.Trim();
        if (trimmedValue.StartsWith("constant", StringComparison.OrdinalIgnoreCase))
        {
            int separatorIndex = trimmedValue.IndexOfAny(new[] { ':', '=' });
            if (separatorIndex < 0)
            {
                gradient = ColorGradient.White;
                return true;
            }

            string colorText = trimmedValue[(separatorIndex + 1)..].Trim();
            if (!TryParseColor(colorText, out Color color, out statusMessage))
            {
                return false;
            }

            gradient = ColorGradient.Constant(color);
            return true;
        }

        switch (NormalizePropertyKey(trimmedValue))
        {
            case "reset":
            case "white":
                gradient = ColorGradient.White;
                return true;
            case "fire":
                gradient = ColorGradient.Fire();
                return true;
            case "smoke":
                gradient = ColorGradient.Smoke();
                return true;
            case "magic":
            case "magicblue":
                gradient = ColorGradient.MagicBlue();
                return true;
            case "spark":
                gradient = ColorGradient.Spark();
                return true;
            case "ember":
                gradient = ColorGradient.Ember();
                return true;
            default:
                statusMessage = $"Unknown gradient preset '{rawValue}'.";
                return false;
        }
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

    private static string DescribeCurve(FloatCurve curve)
    {
        if (curve.Keys.Count == 0)
        {
            return "keys=0";
        }

        FloatCurveKey firstKey = curve.Keys[0];
        FloatCurveKey lastKey = curve.Keys[curve.Keys.Count - 1];
        return string.Create(CultureInfo.InvariantCulture,
            $"keys={curve.Keys.Count} first=({firstKey.Time:0.###},{firstKey.Value:0.###}) last=({lastKey.Time:0.###},{lastKey.Value:0.###})");
    }

    private static string DescribeGradient(ColorGradient gradient)
    {
        Color firstColor = gradient.Evaluate(0.0f);
        Color lastColor = gradient.Evaluate(1.0f);
        return string.Create(CultureInfo.InvariantCulture,
            $"colors={gradient.ColorKeys.Count} alphas={gradient.AlphaKeys.Count} first=({firstColor.R},{firstColor.G},{firstColor.B},{firstColor.A}) last=({lastColor.R},{lastColor.G},{lastColor.B},{lastColor.A})");
    }

    private static FloatCurve CloneCurve(FloatCurve curve)
    {
        var clone = new FloatCurve();
        for (int keyIndex = 0; keyIndex < curve.Keys.Count; keyIndex++)
        {
            clone.AddKey(curve.Keys[keyIndex]);
        }

        return clone;
    }

    private static FloatCurve AddCurveKey(FloatCurve curve)
    {
        var result = CloneCurve(curve);
        result.AddKey(1.0f, curve.Keys.Count == 0 ? 1.0f : curve.Keys[curve.Keys.Count - 1].Value);
        return result;
    }

    private static FloatCurve ReplaceCurveKey(FloatCurve curve, int keyIndex, float time, float value)
    {
        var result = new FloatCurve();
        for (int index = 0; index < curve.Keys.Count; index++)
        {
            FloatCurveKey key = curve.Keys[index];
            result.AddKey(index == keyIndex ? new FloatCurveKey(time, value) : key);
        }

        return result;
    }

    private static FloatCurve RemoveCurveKey(FloatCurve curve, int keyIndex)
    {
        var result = new FloatCurve();
        for (int index = 0; index < curve.Keys.Count; index++)
        {
            if (index != keyIndex)
            {
                result.AddKey(curve.Keys[index]);
            }
        }

        return result;
    }

    private static FloatCurveKey GetCurveKeyOrFallback(FloatCurve curve, int keyIndex, FloatCurveKey fallback)
        => keyIndex >= 0 && keyIndex < curve.Keys.Count ? curve.Keys[keyIndex] : fallback;

    private static ColorGradient CloneGradient(ColorGradient gradient)
    {
        var clone = new ColorGradient();
        for (int keyIndex = 0; keyIndex < gradient.ColorKeys.Count; keyIndex++)
        {
            clone.AddColorKey(gradient.ColorKeys[keyIndex]);
        }

        for (int keyIndex = 0; keyIndex < gradient.AlphaKeys.Count; keyIndex++)
        {
            clone.AddAlphaKey(gradient.AlphaKeys[keyIndex]);
        }

        return clone;
    }

    private static ColorGradient AddGradientColorKey(ColorGradient gradient)
    {
        var result = CloneGradient(gradient);
        Color color = gradient.ColorKeys.Count == 0 ? Color.White : gradient.ColorKeys[gradient.ColorKeys.Count - 1].Color;
        result.AddColorKey(1.0f, color);
        return result;
    }

    private static ColorGradient AddGradientAlphaKey(ColorGradient gradient)
    {
        var result = CloneGradient(gradient);
        float alpha = gradient.AlphaKeys.Count == 0 ? 1.0f : gradient.AlphaKeys[gradient.AlphaKeys.Count - 1].Alpha;
        result.AddAlphaKey(1.0f, alpha);
        return result;
    }

    private static ColorGradient ReplaceGradientColorKey(ColorGradient gradient, int keyIndex, float time, Color color)
    {
        var result = new ColorGradient();
        for (int index = 0; index < gradient.ColorKeys.Count; index++)
        {
            ColorGradientKey key = gradient.ColorKeys[index];
            result.AddColorKey(index == keyIndex ? new ColorGradientKey(time, color) : key);
        }

        for (int index = 0; index < gradient.AlphaKeys.Count; index++)
        {
            result.AddAlphaKey(gradient.AlphaKeys[index]);
        }

        return result;
    }

    private static ColorGradient ReplaceGradientAlphaKey(ColorGradient gradient, int keyIndex, float time, float alpha)
    {
        var result = new ColorGradient();
        for (int index = 0; index < gradient.ColorKeys.Count; index++)
        {
            result.AddColorKey(gradient.ColorKeys[index]);
        }

        for (int index = 0; index < gradient.AlphaKeys.Count; index++)
        {
            AlphaGradientKey key = gradient.AlphaKeys[index];
            result.AddAlphaKey(index == keyIndex ? new AlphaGradientKey(time, alpha) : key);
        }

        return result;
    }

    private static ColorGradient RemoveGradientColorKey(ColorGradient gradient, int keyIndex)
    {
        var result = new ColorGradient();
        for (int index = 0; index < gradient.ColorKeys.Count; index++)
        {
            if (index != keyIndex)
            {
                result.AddColorKey(gradient.ColorKeys[index]);
            }
        }

        for (int index = 0; index < gradient.AlphaKeys.Count; index++)
        {
            result.AddAlphaKey(gradient.AlphaKeys[index]);
        }

        return result;
    }

    private static ColorGradient RemoveGradientAlphaKey(ColorGradient gradient, int keyIndex)
    {
        var result = new ColorGradient();
        for (int index = 0; index < gradient.ColorKeys.Count; index++)
        {
            result.AddColorKey(gradient.ColorKeys[index]);
        }

        for (int index = 0; index < gradient.AlphaKeys.Count; index++)
        {
            if (index != keyIndex)
            {
                result.AddAlphaKey(gradient.AlphaKeys[index]);
            }
        }

        return result;
    }

    private static ColorGradientKey GetColorKeyOrFallback(ColorGradient gradient, int keyIndex, ColorGradientKey fallback)
        => keyIndex >= 0 && keyIndex < gradient.ColorKeys.Count ? gradient.ColorKeys[keyIndex] : fallback;

    private static AlphaGradientKey GetAlphaKeyOrFallback(ColorGradient gradient, int keyIndex, AlphaGradientKey fallback)
        => keyIndex >= 0 && keyIndex < gradient.AlphaKeys.Count ? gradient.AlphaKeys[keyIndex] : fallback;

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

    private static string SerializeParticleAsset(ParticleEffectAsset particleAsset)
    {
        var document = new JObject();
        ParticleEffectAssetJsonSerializer.Save(particleAsset, document);
        return document.ToString(Formatting.None);
    }
}