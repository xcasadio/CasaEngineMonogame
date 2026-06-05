#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Controls;

internal sealed class SpriteAssetInspectorPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly PreviewWorldDriver _previewWorldDriver;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly IWindowInputSource _windowInputSource;

    private MGDockPanel? _inspectorRoot;
    private MGTextBlock? _headerText;
    private MGTextBlock? _sourceText;
    private MGTextBlock? _statusText;
    private MGStackPanel? _contentStack;
    private WorldViewportPanel? _previewViewportPanel;
    private Entity? _previewEntity;
    private EditorSpritePreviewComponent? _previewSpriteComponent;

    private SpriteData? _spriteData;
    private string? _loadedRelativePath;
    private string? _historyContextId;
    private bool _isDirty;
    private bool _disposed;

    public SpriteAssetInspectorPanel(
        MGWindow window,
        GraphicsDevice graphicsDevice,
        HostedEditorGameAdapter editorRuntime,
        IWindowInputSource windowInputSource)
    {
        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _windowInputSource = windowInputSource;
        _previewWorldDriver = new PreviewWorldDriver(
            editorRuntime,
            new PreviewWorldDriverOptions
            {
                WorldName = "Sprite Preview",
                UpdateMode = PreviewWorldUpdateMode.Manual,
            });
    }

    public SpriteData? LoadedSpriteData => _spriteData;

    public string? LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    public event Action<SpriteAssetInspectorPanel>? DirtyStateChanged;

    public MGElement CreateContent()
    {
        if (_inspectorRoot != null)
        {
            return _inspectorRoot;
        }

        _headerText = new MGTextBlock(_window, "[b]Sprite Inspector[/b]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        _sourceText = new MGTextBlock(_window, "No sprite asset loaded.")
        {
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.8f,
            WrapText = true,
        };

        _statusText = new MGTextBlock(_window, "Open a .sprite asset from the Content Browser.")
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

        _contentStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
        };

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(_contentStack);

        _inspectorRoot = new MGDockPanel(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _inspectorRoot.TryAddChild(_headerText, Dock.Top);
        _inspectorRoot.TryAddChild(_sourceText, Dock.Top);
        _inspectorRoot.TryAddChild(_statusText, Dock.Top);
        _inspectorRoot.TryAddChild(toolbar, Dock.Top);
        _inspectorRoot.TryAddChild(scrollViewer, Dock.Top);

        RefreshInspector();
        return _inspectorRoot;
    }

    public MGElement CreateDocumentContent()
    {
        if (_previewViewportPanel != null)
        {
            return _previewViewportPanel.CreateContent();
        }

        _previewViewportPanel = new WorldViewportPanel(_window, _graphicsDevice, _editorRuntime, _windowInputSource)
        {
            EnablePreviewSelection = false,
            EnablePreviewGizmo = false,
            ShowEditorOverlays = false,
            UseFront2dCamera = true,
        };

        if (_previewWorldDriver.World != null)
        {
            _previewViewportPanel.SetWorldOverride(_previewWorldDriver.World);
            FocusPreview();
        }

        return _previewViewportPanel.CreateContent();
    }

    public void SetHistoryContextId(string historyContextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyContextId);
        _historyContextId = historyContextId;
    }

    public void LoadAsset(SpriteData spriteData, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(spriteData);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        _spriteData = spriteData;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        CacheLoadedSpriteAsset();
        EnsurePreviewWorldCreated();
        RefreshPreviewState();
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
        if (!TryLoadAsset(fullPath, out var spriteData))
        {
            return false;
        }

        LoadAsset(spriteData, fullPath);
        return true;
    }

    public bool TrySaveLoadedAsset(out string? errorMessage)
    {
        errorMessage = null;

        if (_spriteData == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No sprite asset is loaded.";
            return false;
        }

        if (!IsDirty)
        {
            SetStatus($"Already saved {_loadedRelativePath}");
            return true;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _spriteData, EditorAssetSaveSource.SpriteEditorPanel);
            CacheLoadedSpriteAsset();
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

    public void Update(GameTime gameTime)
    {
        _previewWorldDriver.Tick(gameTime);
    }

    public void DrawViewport(GameTime gameTime)
    {
        _previewViewportPanel?.DrawViewport(gameTime);
    }

    public IReadOnlyList<string> GetAutomationStateSnapshot()
    {
        if (_spriteData == null)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>
        {
            $"Name: {_spriteData.Name}",
            $"Path: {_loadedRelativePath ?? "<none>"}",
            $"Dirty: {IsDirty}",
            $"Sprite sheet: {FormatAssetReference(_spriteData.SpriteSheetAssetId)}",
            $"Location: {_spriteData.PositionInTexture.X},{_spriteData.PositionInTexture.Y},{_spriteData.PositionInTexture.Width},{_spriteData.PositionInTexture.Height}",
            $"Origin: {_spriteData.Origin.X},{_spriteData.Origin.Y}",
            $"Sockets: {_spriteData.Sockets.Count}",
            $"Collisions: {_spriteData.CollisionShapes.Count}",
        };

        var previewStates = _previewViewportPanel?.GetAutomationStateSnapshot() ?? Array.Empty<string>();
        for (int index = 0; index < previewStates.Count; index++)
        {
            result.Add($"Preview {previewStates[index]}");
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _previewViewportPanel?.Dispose();
        _previewWorldDriver.Dispose();
    }

    public static bool TryLoadAsset(string fullPath, out SpriteData spriteData)
    {
        spriteData = new SpriteData();

        if (!File.Exists(fullPath)
            || !string.Equals(Path.GetExtension(fullPath), Constants.FileNameExtensions.Sprite, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = JObject.Parse(File.ReadAllText(fullPath));
            if (document["sprite_sheet_asset_id"] == null
                || document["location"] == null
                || document["hotspot"] == null)
            {
                return false;
            }

            spriteData.Load(document);
            spriteData.FileName = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);

            var assetInfo = AssetCatalog.GetByFileName(spriteData.FileName)
                            ?? AssetCatalog.GetByFileName(spriteData.FileName.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (assetInfo != null)
            {
                spriteData.Name = assetInfo.Name;
                spriteData.AssetId = assetInfo.Id;
                spriteData.FileName = assetInfo.FileName;
            }
            else
            {
                spriteData.AssetId = spriteData.Id;
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
            SetStatus("Unable to reload sprite asset.");
        }
    }

    private void RefreshInspector()
    {
        if (_contentStack == null || _headerText == null || _sourceText == null || _statusText == null)
        {
            return;
        }

        _contentStack.TryRemoveAll();
        if (_spriteData == null)
        {
            _headerText.Text = "[b]Sprite Inspector[/b]";
            _sourceText.Text = "No sprite asset loaded.";
            _statusText.Text = "Open a .sprite asset from the Content Browser.";
            return;
        }

        RefreshHeaderText();
        RefreshSourceText();
        RefreshStatusText();

        _contentStack.TryAddChild(BuildSectionHeader("Asset"));
        _contentStack.TryAddChild(BuildPropertyRow("Name", CreateTextBox(_spriteData.Name, value => ApplyChange(() =>
        {
            _spriteData.Name = string.IsNullOrWhiteSpace(value) ? "Sprite" : value;
        }, "Name", RefreshHeaderText))));
        _contentStack.TryAddChild(BuildPropertyRow("Sprite Sheet", CreateTextureSelectorRow()));

        _contentStack.TryAddChild(BuildSectionHeader("Texture Rect"));
        _contentStack.TryAddChild(BuildPropertyRow("X", CreateIntField(_spriteData.PositionInTexture.X, -32768, 32768, value => ApplyChange(() =>
            _spriteData.PositionInTexture = new Rectangle(value, _spriteData.PositionInTexture.Y, _spriteData.PositionInTexture.Width, _spriteData.PositionInTexture.Height),
            "Location X",
            RefreshPreviewState))));
        _contentStack.TryAddChild(BuildPropertyRow("Y", CreateIntField(_spriteData.PositionInTexture.Y, -32768, 32768, value => ApplyChange(() =>
            _spriteData.PositionInTexture = new Rectangle(_spriteData.PositionInTexture.X, value, _spriteData.PositionInTexture.Width, _spriteData.PositionInTexture.Height),
            "Location Y",
            RefreshPreviewState))));
        _contentStack.TryAddChild(BuildPropertyRow("Width", CreateIntField(_spriteData.PositionInTexture.Width, 1, 32768, value => ApplyChange(() =>
            _spriteData.PositionInTexture = new Rectangle(_spriteData.PositionInTexture.X, _spriteData.PositionInTexture.Y, value, _spriteData.PositionInTexture.Height),
            "Location Width",
            RefreshPreviewState))));
        _contentStack.TryAddChild(BuildPropertyRow("Height", CreateIntField(_spriteData.PositionInTexture.Height, 1, 32768, value => ApplyChange(() =>
            _spriteData.PositionInTexture = new Rectangle(_spriteData.PositionInTexture.X, _spriteData.PositionInTexture.Y, _spriteData.PositionInTexture.Width, value),
            "Location Height",
            RefreshPreviewState))));

        _contentStack.TryAddChild(BuildSectionHeader("Origin"));
        _contentStack.TryAddChild(BuildPropertyRow("Origin X", CreateIntField(_spriteData.Origin.X, -32768, 32768, value => ApplyChange(() =>
            _spriteData.Origin = new Point(value, _spriteData.Origin.Y),
            "Origin X",
            RefreshPreviewState))));
        _contentStack.TryAddChild(BuildPropertyRow("Origin Y", CreateIntField(_spriteData.Origin.Y, -32768, 32768, value => ApplyChange(() =>
            _spriteData.Origin = new Point(_spriteData.Origin.X, value),
            "Origin Y",
            RefreshPreviewState))));

        _contentStack.TryAddChild(BuildSectionHeader($"Sockets ({_spriteData.Sockets.Count})"));
        _contentStack.TryAddChild(BuildPropertyRow("Sockets", CreateButton("Add Socket", AddSocket)));
        if (_spriteData.Sockets.Count == 0)
        {
            _contentStack.TryAddChild(BuildText("No sockets in asset."));
        }
        else
        {
            for (int socketIndex = 0; socketIndex < _spriteData.Sockets.Count; socketIndex++)
            {
                BuildSocketEditor(_spriteData.Sockets[socketIndex], socketIndex);
            }
        }

        _contentStack.TryAddChild(BuildSectionHeader($"Collisions ({_spriteData.CollisionShapes.Count})"));
        var collisionButtons = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };
        collisionButtons.TryAddChild(CreateButton("Add Rectangle", AddRectangleCollision));
        collisionButtons.TryAddChild(CreateButton("Add Circle", AddCircleCollision));
        _contentStack.TryAddChild(BuildPropertyRow("Collision Tools", collisionButtons));
        if (_spriteData.CollisionShapes.Count == 0)
        {
            _contentStack.TryAddChild(BuildText("No collision shapes in asset."));
        }
        else
        {
            for (int collisionIndex = 0; collisionIndex < _spriteData.CollisionShapes.Count; collisionIndex++)
            {
                BuildCollisionEditor(_spriteData.CollisionShapes[collisionIndex], collisionIndex);
            }
        }
    }

    private void BuildSocketEditor(Socket socket, int socketIndex)
    {
        if (_contentStack == null)
        {
            return;
        }

        _contentStack.TryAddChild(BuildSectionHeader($"Socket {socketIndex + 1}"));
        _contentStack.TryAddChild(BuildPropertyRow("Name", CreateTextBox(socket.Name, value => ApplyChange(() => socket.Name = value, "Socket Name"))));
        _contentStack.TryAddChild(BuildPropertyRow("X", CreateIntField(socket.Position.X, -32768, 32768, value => ApplyChange(() => socket.Position = new Point(value, socket.Position.Y), "Socket X"))));
        _contentStack.TryAddChild(BuildPropertyRow("Y", CreateIntField(socket.Position.Y, -32768, 32768, value => ApplyChange(() => socket.Position = new Point(socket.Position.X, value), "Socket Y"))));
        _contentStack.TryAddChild(BuildPropertyRow("Actions", CreateButton("Remove Socket", () => RemoveSocket(socket))));
    }

    private void BuildCollisionEditor(Collision2d collision, int collisionIndex)
    {
        if (_contentStack == null)
        {
            return;
        }

        _contentStack.TryAddChild(BuildSectionHeader($"Collision {collisionIndex + 1}"));
        _contentStack.TryAddChild(BuildPropertyRow("Hit Type", CreateEnumCombo(collision.CollisionHitType, value => ApplyChange(() => collision.CollisionHitType = value, "Collision Type"))));

        switch (collision.Shape)
        {
            case ShapeRectangle rectangle:
                _contentStack.TryAddChild(BuildPropertyRow("Shape", BuildText("Rectangle")));
                _contentStack.TryAddChild(BuildPropertyRow("X", CreateFloatField(rectangle.Position.X, -32768f, 32768f, 1f, value => ApplyChange(() => rectangle.Position = new Vector2(value, rectangle.Position.Y), "Collision X"))));
                _contentStack.TryAddChild(BuildPropertyRow("Y", CreateFloatField(rectangle.Position.Y, -32768f, 32768f, 1f, value => ApplyChange(() => rectangle.Position = new Vector2(rectangle.Position.X, value), "Collision Y"))));
                _contentStack.TryAddChild(BuildPropertyRow("Width", CreateFloatField(rectangle.Width, 0.01f, 32768f, 1f, value => ApplyChange(() => rectangle.Width = value, "Collision Width"))));
                _contentStack.TryAddChild(BuildPropertyRow("Height", CreateFloatField(rectangle.Height, 0.01f, 32768f, 1f, value => ApplyChange(() => rectangle.Height = value, "Collision Height"))));
                _contentStack.TryAddChild(BuildPropertyRow("Rotation", CreateFloatField(rectangle.Rotation, -360f, 360f, 1f, value => ApplyChange(() => rectangle.Rotation = value, "Collision Rotation"))));
                break;

            case ShapeCircle circle:
                _contentStack.TryAddChild(BuildPropertyRow("Shape", BuildText("Circle")));
                _contentStack.TryAddChild(BuildPropertyRow("X", CreateFloatField(circle.Position.X, -32768f, 32768f, 1f, value => ApplyChange(() => circle.Position = new Vector2(value, circle.Position.Y), "Collision X"))));
                _contentStack.TryAddChild(BuildPropertyRow("Y", CreateFloatField(circle.Position.Y, -32768f, 32768f, 1f, value => ApplyChange(() => circle.Position = new Vector2(circle.Position.X, value), "Collision Y"))));
                _contentStack.TryAddChild(BuildPropertyRow("Radius", CreateFloatField(circle.Radius, 0.01f, 32768f, 1f, value => ApplyChange(() => circle.Radius = value, "Collision Radius"))));
                _contentStack.TryAddChild(BuildPropertyRow("Rotation", CreateFloatField(circle.Rotation, -360f, 360f, 1f, value => ApplyChange(() => circle.Rotation = value, "Collision Rotation"))));
                break;

            default:
                _contentStack.TryAddChild(BuildText($"Unsupported collision shape: {collision.Shape.GetType().Name}"));
                break;
        }

        _contentStack.TryAddChild(BuildPropertyRow("Actions", CreateButton("Remove Collision", () => RemoveCollision(collision))));
    }

    private void AddSocket()
    {
        if (_spriteData == null)
        {
            return;
        }

        ApplyChange(() => _spriteData.Sockets.Add(new Socket
        {
            Name = $"Socket{_spriteData.Sockets.Count + 1}",
            Position = Point.Zero,
        }), "Add Socket", RefreshInspector);
    }

    private void RemoveSocket(Socket socket)
    {
        if (_spriteData == null)
        {
            return;
        }

        ApplyChange(() => _spriteData.Sockets.Remove(socket), "Remove Socket", RefreshInspector);
    }

    private void AddRectangleCollision()
    {
        if (_spriteData == null)
        {
            return;
        }

        ApplyChange(() => _spriteData.CollisionShapes.Add(new Collision2d
        {
            CollisionHitType = CollisionHitType.Unknown,
            Shape = new ShapeRectangle(16f, 16f),
        }), "Add Rectangle Collision", RefreshInspector);
    }

    private void AddCircleCollision()
    {
        if (_spriteData == null)
        {
            return;
        }

        ApplyChange(() => _spriteData.CollisionShapes.Add(new Collision2d
        {
            CollisionHitType = CollisionHitType.Unknown,
            Shape = new ShapeCircle { Radius = 8f },
        }), "Add Circle Collision", RefreshInspector);
    }

    private void RemoveCollision(Collision2d collision)
    {
        if (_spriteData == null)
        {
            return;
        }

        ApplyChange(() => _spriteData.CollisionShapes.Remove(collision), "Remove Collision", RefreshInspector);
    }

    private void EnsurePreviewWorldCreated()
    {
        if (_spriteData == null || _previewWorldDriver.World != null)
        {
            return;
        }

        _previewWorldDriver.Rebuild(world =>
        {
            _previewEntity = new Entity
            {
                Name = string.IsNullOrWhiteSpace(_spriteData.Name) ? "Sprite Preview" : _spriteData.Name,
            };

            _previewSpriteComponent = new EditorSpritePreviewComponent
            {
                Color = Color.White,
                SpriteEffect = SpriteEffects.None,
            };

            _previewEntity.RootComponent = _previewSpriteComponent;
            world.AddEntity(_previewEntity);
        });
    }

    private void RefreshPreviewState()
    {
        if (_spriteData == null)
        {
            _previewWorldDriver.Clear();
            _previewViewportPanel?.SetWorldOverride(null);
            _previewEntity = null;
            _previewSpriteComponent = null;
            RefreshStatusText();
            return;
        }

        CacheLoadedSpriteAsset();
        EnsurePreviewWorldCreated();
        if (_previewEntity != null)
        {
            _previewEntity.Name = string.IsNullOrWhiteSpace(_spriteData.Name) ? "Sprite Preview" : _spriteData.Name;
        }

        _previewSpriteComponent?.SetSpriteData(_spriteData);
        _previewWorldDriver.RefreshNow();
        if (_previewViewportPanel != null)
        {
            _previewViewportPanel.SetWorldOverride(_previewWorldDriver.World);
            FocusPreview();
        }

        RefreshStatusText();
    }

    private void FocusPreview()
    {
        if (_previewViewportPanel == null || _spriteData == null)
        {
            return;
        }

        _previewViewportPanel.FocusBounds(SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData));
    }

    private void CacheLoadedSpriteAsset()
    {
        if (_spriteData == null)
        {
            return;
        }

        Guid spriteAssetId = GetLoadedSpriteAssetId();
        var assetInfo = !string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? AssetCatalog.GetByFileName(_loadedRelativePath)
              ?? AssetCatalog.GetByFileName(_loadedRelativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : null;

        if (assetInfo != null)
        {
            _spriteData.AssetId = assetInfo.Id;
            _spriteData.Name = assetInfo.Name;
            _spriteData.FileName = assetInfo.FileName;
            _editorRuntime.AssetContentManager.AddAsset(assetInfo, _spriteData);
            return;
        }

        _editorRuntime.AssetContentManager.AddAsset(spriteAssetId, _spriteData?.Name ?? string.Empty, _spriteData);
    }

    private Guid GetLoadedSpriteAssetId()
        => _spriteData?.AssetId != Guid.Empty ? _spriteData.AssetId : _spriteData?.Id ?? Guid.Empty;

    private void ApplyChange(Action applyChange, string label, Action? afterApply = null)
    {
        if (_spriteData == null)
        {
            return;
        }

        applyChange();
        SetDirty(true);
        RefreshPreviewState();
        afterApply?.Invoke();
        SetStatus($"{label} updated.");
    }

    private bool TryGetHistoryContext(out EditorHistoryContext historyContext)
    {
        if (string.IsNullOrWhiteSpace(_historyContextId))
        {
            historyContext = EditorHistoryContext.Empty;
            return false;
        }

        historyContext = new EditorHistoryContext(EditorHistoryContextKind.Sprite, _historyContextId);
        return true;
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

    private void RefreshHeaderText()
    {
        if (_headerText == null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(_spriteData?.Name) ? "Sprite" : _spriteData.Name;
        _headerText.Text = $"[b]Sprite Inspector[/b]  [opacity=0.7]{EscapeMarkup(title)}[/opacity]";
    }

    private void RefreshSourceText()
    {
        if (_sourceText == null)
        {
            return;
        }

        _sourceText.Text = string.IsNullOrWhiteSpace(_loadedRelativePath)
            ? "No sprite asset loaded."
            : $"Source: {EscapeMarkup(_loadedRelativePath)}";
    }

    private void RefreshStatusText()
    {
        if (_spriteData == null)
        {
            SetStatus("Open a .sprite asset from the Content Browser.");
            return;
        }

        SetStatus($"Sprite ready. Size: {_spriteData.PositionInTexture.Width} x {_spriteData.PositionInTexture.Height}.");
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = EscapeMarkup(message);
        }
    }

    private MGElement BuildSectionHeader(string text)
    {
        return new MGTextBlock(_window, $"[b]{EscapeMarkup(text)}[/b]")
        {
            Margin = new Thickness(0, 6, 0, 2),
            WrapText = true,
        };
    }

    private MGElement BuildText(string text)
    {
        return new MGTextBlock(_window, EscapeMarkup(text))
        {
            Opacity = 0.85f,
            WrapText = true,
        };
    }

    private MGElement BuildPropertyRow(string label, MGElement editor)
    {
        var row = new MGDockPanel(_window)
        {
            Margin = new Thickness(0, 0, 0, 2),
        };
        row.TryAddChild(new MGTextBlock(_window, EscapeMarkup(label))
        {
            PreferredWidth = 140,
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
            if (!string.IsNullOrWhiteSpace(args.NewValue)
                && Enum.TryParse(args.NewValue, out TEnum parsedValue))
            {
                onChanged(parsedValue);
            }
        };
        return combo;
    }

    private MGElement CreateTextureSelectorRow()
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
        };

        var selector = new AssetSelector(_window)
        {
            AssetId = _spriteData?.SpriteSheetAssetId ?? Guid.Empty,
            Filter = IsTextureAsset,
        };
        selector.AssetChanged += (_, assetId) => ApplyChange(() =>
            _spriteData!.SpriteSheetAssetId = assetId,
            "Sprite Sheet",
            RefreshPreviewState);
        row.TryAddChild(selector);
        row.TryAddChild(CreateButton("Clear", () =>
        {
            selector.AssetId = Guid.Empty;
            ApplyChange(() => _spriteData!.SpriteSheetAssetId = Guid.Empty, "Sprite Sheet", RefreshPreviewState);
        }));
        return row;
    }

    private bool IsTextureAsset(AssetInfo assetInfo)
        => assetInfo != null
        && string.Equals(Path.GetExtension(assetInfo.FileName), Constants.FileNameExtensions.Texture, StringComparison.OrdinalIgnoreCase);

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 120,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private static string FormatAssetReference(Guid assetId)
    {
        if (assetId == Guid.Empty)
        {
            return "<none>";
        }

        var assetInfo = AssetCatalog.Get(assetId);
        return assetInfo == null ? assetId.ToString() : assetInfo.Name;
    }

    private static string EscapeMarkup(string value)
        => value.Replace("[", "\\[").Replace("]", "\\]");
}