using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Textures;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Text;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework.Graphics;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Document for a <c>.texture</c> asset (plan P7): a preview of the catalogued image the texture samples, and the sampler
/// state (filter, address modes U, V and W, maximum anisotropy), saved through <see cref="EditorAssetWriterService"/>.
/// </summary>
/// <remarks>
/// The edits go through <see cref="TextureAssetDocument"/>, which builds a new <see cref="SamplerState"/> for each change and
/// never alters the previous one. The preview texture is created when the content is built and released by
/// <see cref="Dispose"/>, which the editor calls when the tab closes.
/// </remarks>
internal sealed class TextureAssetDocumentPanel : IDisposable
{
    private static readonly string[] FilterNames = Enum.GetNames<TextureFilter>();
    private static readonly string[] AddressNames = Enum.GetNames<TextureAddressMode>();
    private static readonly string[] AnisotropyNames = CreateAnisotropyNames();

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;

    private MGDockPanel _root;
    private MGTextBlock _headerText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGStackPanel _fieldStack;

    private TextureAssetDocument _document;
    private string _loadedRelativePath;
    private Texture2D _previewTexture;
    private bool _isDirty;
    private bool _suppressControlCallbacks;
    private bool _disposed;

    public TextureAssetDocumentPanel(MGWindow window, GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _graphicsDevice = graphicsDevice;
    }

    public TextureAssetDocument Document => _document;

    public string LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    /// <summary>Text of the status line.</summary>
    public string StatusMessage => _statusText?.Text ?? string.Empty;

    public event Action<TextureAssetDocumentPanel> DirtyStateChanged;

    public static bool TryLoadAsset(string fullPath, out TextureAssetDocument document, out string error)
    {
        document = null;
        if (!File.Exists(fullPath)
            || !Path.GetExtension(fullPath).Equals(Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase))
        {
            error = $"'{fullPath}' is not a {Constants.FileNameExtensions.Texture} file.";
            return false;
        }

        return TextureAssetDocument.TryLoad(fullPath, out document, out error);
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Texture[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, string.Empty)
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
        toolbar.TryAddChild(CreateButton("Save", () => TrySaveLoadedAsset(out _)));
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

    public void LoadAsset(TextureAssetDocument document, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _document = document;
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
        if (!TryLoadAsset(fullPath, out TextureAssetDocument document, out string error))
        {
            SetStatus(error);
            return false;
        }

        LoadAsset(document, fullPath);
        SetStatus($"Reloaded {_loadedRelativePath}");
        return true;
    }

    /// <summary>Changes one sampler field and marks the document modified; nothing changes on failure.</summary>
    public bool TryApplySamplerEdit(string key, string value, out string error)
    {
        if (_document == null)
        {
            error = "No texture asset is loaded.";
            return false;
        }

        if (!_document.TryApply(key, value, out error))
        {
            SetStatus($"Cannot set {key}: {error}");
            return false;
        }

        SetDirty(true);
        SetStatus($"{key} = {TextureAssetDocument.Format(_document.Sampler, key)} (not saved)");
        return true;
    }

    public bool TrySaveLoadedAsset(out string errorMessage)
    {
        errorMessage = null;

        if (_document == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No texture asset is loaded.";
            return false;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _document.Texture, EditorAssetSaveSource.TextureEditorPanel);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleasePreview();
    }

    private void RefreshInspector()
    {
        if (_fieldStack == null)
        {
            return;
        }

        _fieldStack.TryRemoveAll();
        ReleasePreview();

        if (_document == null)
        {
            _headerText.Text = "[b]Texture[/b]";
            _sourceText.Text = "No texture asset loaded.";
            return;
        }

        _headerText.Text = "[b]" + FTTokenizer.EscapeMarkdown(_document.Texture.Name ?? string.Empty) + "[/b]";
        _sourceText.Text = FTTokenizer.EscapeMarkdown(_loadedRelativePath ?? string.Empty);

        AssetInfo imageInfo = _document.ImageAssetId != Guid.Empty ? AssetCatalog.Get(_document.ImageAssetId) : null;
        _fieldStack.TryAddChild(new MGTextBlock(_window,
            "[opacity=0.65]Image:[/opacity]  " + FTTokenizer.EscapeMarkdown(imageInfo?.FileName ?? $"{_document.ImageAssetId} (not in the asset catalog)"))
        {
            WrapText = true,
        });

        _suppressControlCallbacks = true;
        try
        {
            SamplerState sampler = _document.Sampler;
            _fieldStack.TryAddChild(CreateChoiceRow("Filter", TextureAssetDocument.FilterKey, FilterNames, sampler.Filter.ToString()));
            _fieldStack.TryAddChild(CreateChoiceRow("Address U", TextureAssetDocument.AddressUKey, AddressNames, sampler.AddressU.ToString()));
            _fieldStack.TryAddChild(CreateChoiceRow("Address V", TextureAssetDocument.AddressVKey, AddressNames, sampler.AddressV.ToString()));
            _fieldStack.TryAddChild(CreateChoiceRow("Address W", TextureAssetDocument.AddressWKey, AddressNames, sampler.AddressW.ToString()));
            _fieldStack.TryAddChild(CreateChoiceRow("Max anisotropy", TextureAssetDocument.MaxAnisotropyKey, AnisotropyNames,
                TextureAssetDocument.Format(sampler, TextureAssetDocument.MaxAnisotropyKey)));
        }
        finally
        {
            _suppressControlCallbacks = false;
        }

        // Under the controls, so that they stay in view whatever the size of the image.
        MGElement preview = BuildImagePreview(imageInfo);
        if (preview != null)
        {
            _fieldStack.TryAddChild(preview);
        }
    }

    private MGElement BuildImagePreview(AssetInfo imageInfo)
    {
        if (_graphicsDevice == null || imageInfo == null || string.IsNullOrWhiteSpace(imageInfo.FileName))
        {
            return null;
        }

        string imagePath = Path.Combine(EngineEnvironment.ProjectPath, imageInfo.FileName);
        try
        {
            using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _previewTexture = Texture2D.FromStream(_graphicsDevice, stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            SetStatus($"The image cannot be shown: '{imagePath}' could not be decoded ({exception.Message}).");
            Logs.WriteWarning($"Texture preview of '{imagePath}': {exception.Message}");
            return null;
        }

        return new MGImage(_window, new MGTextureData(EditorIcons.AsImage(_previewTexture)!), Stretch: Stretch.Uniform)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 200,
            IsHitTestVisible = false,
        };
    }

    private MGElement CreateChoiceRow(string label, string key, string[] choices, string selected)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        row.TryAddChild(new MGTextBlock(_window, label)
        {
            PreferredWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var combo = new MGComboBox<string>(_window)
        {
            MinWidth = 180,
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
        combo.SetItemsSource(choices);
        combo.SelectedItem = selected;
        combo.SelectedItemChanged += (_, args) =>
        {
            if (_suppressControlCallbacks || string.IsNullOrWhiteSpace(args.NewValue))
            {
                return;
            }

            TryApplySamplerEdit(key, args.NewValue, out string _);
        };

        row.TryAddChild(combo);
        return row;
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

    private void ReleasePreview()
    {
        _previewTexture?.Dispose();
        _previewTexture = null;
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

    private static string[] CreateAnisotropyNames()
    {
        var names = new string[TextureAssetDocument.MaxMaxAnisotropy - TextureAssetDocument.MinMaxAnisotropy + 1];
        for (int index = 0; index < names.Length; index++)
        {
            names[index] = (TextureAssetDocument.MinMaxAnisotropy + index).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return names;
    }
}
