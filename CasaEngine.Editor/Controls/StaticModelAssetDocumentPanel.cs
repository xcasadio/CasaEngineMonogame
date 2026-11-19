using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.StaticModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Rendering.Models;
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
/// Document for a <c>.staticModel</c> asset (plan P8): a 3D preview framed on the model by <see cref="ModelPreviewViewport"/>,
/// the list of its meshes and sub-meshes with their material, and the choice of a material among the catalogued
/// <c>material</c> assets, saved through <see cref="EditorAssetWriterService"/>.
/// </summary>
/// <remarks>
/// The edits go through <see cref="StaticModelAssetDocument"/>, which changes <c>material_asset_id</c> and nothing else: the
/// geometry is saved as it was read. The panel owns the GPU buffers of the previewed model and the viewport; both are released
/// by <see cref="Dispose"/>, which the editor calls when the tab closes.
/// </remarks>
internal sealed class StaticModelAssetDocumentPanel : IDisposable
{
    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;

    private MGDockPanel _root;
    private MGTextBlock _headerText;
    private MGTextBlock _sourceText;
    private MGTextBlock _statusText;
    private MGStackPanel _meshStack;
    private ModelPreviewViewport _viewport;

    private StaticModelAssetDocument _document;
    private string _loadedRelativePath;
    private bool _isDirty;
    private bool _disposed;

    public StaticModelAssetDocumentPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
    }

    public StaticModelAssetDocument Document => _document;

    public string LoadedRelativePath => _loadedRelativePath;

    public bool IsDirty => _isDirty;

    /// <summary>Text of the status line.</summary>
    public string StatusMessage => _statusText?.Text ?? string.Empty;

    public event Action<StaticModelAssetDocumentPanel> DirtyStateChanged;

    public static bool TryLoadAsset(string fullPath, out StaticModelAssetDocument document, out string error)
    {
        document = null;
        if (!File.Exists(fullPath)
            || !Path.GetExtension(fullPath).Equals(Constants.FileNameExtensions.StaticModel, StringComparison.OrdinalIgnoreCase))
        {
            error = $"'{fullPath}' is not a {Constants.FileNameExtensions.StaticModel} file.";
            return false;
        }

        return StaticModelAssetDocument.TryLoad(fullPath, out document, out error);
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _headerText = new MGTextBlock(_window, "[b]Static model[/b]")
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
            Margin = new Thickness(8, 0, 8, 4),
            Opacity = 0.7f,
            WrapText = true,
        };

        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 4),
        };
        toolbar.TryAddChild(CreateButton("Save", () => TrySaveLoadedAsset(out _)));
        toolbar.TryAddChild(CreateButton("Reload", () => ReloadFromDisk()));

        _meshStack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 6,
            Margin = new Thickness(8, 0, 8, 8),
        };
        var meshScroll = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto)
        {
            PreferredHeight = 130,
        };
        meshScroll.SetContent(_meshStack);

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_headerText, Dock.Top);
        _root.TryAddChild(_sourceText, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);
        _root.TryAddChild(toolbar, Dock.Top);
        _root.TryAddChild(meshScroll, Dock.Bottom);

        if (_editorRuntime != null && _graphicsDevice != null)
        {
            _viewport = new ModelPreviewViewport(_window, _graphicsDevice, _editorRuntime, minHeight: 300);
            _root.TryAddChild(_viewport.CreateContent(), Dock.Top);
        }
        else
        {
            SetStatus("The 3D preview needs the editor runtime, which is not available.");
        }

        RefreshInspector();
        return _root;
    }

    /// <summary>Shows <paramref name="document"/>; the model of the previous one is released.</summary>
    public void LoadAsset(StaticModelAssetDocument document, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        StaticModel previousModel = _document?.Model;
        _document = document;
        _loadedRelativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        SetDirty(false);
        RefreshInspector();

        // The viewport drops the entity of the previous model first; only then are its buffers released.
        if (previousModel != null && !ReferenceEquals(previousModel, document.Model))
        {
            ReleaseModel(previousModel);
        }
    }

    public bool ReloadFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            return false;
        }

        string fullPath = Path.Combine(EngineEnvironment.ProjectPath, _loadedRelativePath);
        if (!TryLoadAsset(fullPath, out StaticModelAssetDocument document, out string error))
        {
            SetStatus(error);
            return false;
        }

        LoadAsset(document, fullPath);
        SetStatus($"Reloaded {_loadedRelativePath}");
        return true;
    }

    /// <summary>
    /// Sets the material of a mesh from automation text (a mesh index and a catalogued material id) and marks the document
    /// modified; nothing changes on failure.
    /// </summary>
    public bool TryApplyMaterialEdit(string meshIndexText, string materialIdText, out string error)
    {
        if (_document == null)
        {
            error = "No static model asset is loaded.";
            return false;
        }

        if (!_document.TryParseMeshIndex(meshIndexText, out int meshIndex, out error)
            || !StaticModelAssetDocument.TryParseMaterialId(materialIdText, null, out Guid materialId, out error))
        {
            SetStatus($"Cannot set the material: {error}");
            return false;
        }

        if (!TryApplyMaterial(meshIndex, StaticModelAssetDocument.WholeMesh, materialId, out error))
        {
            return false;
        }

        // The rows were built before the edit and no control made it: show the new material in them.
        string status = StatusMessage;
        RefreshInspector();
        SetStatus(status);
        return true;
    }

    public bool TrySaveLoadedAsset(out string errorMessage)
    {
        errorMessage = null;

        if (_document == null || string.IsNullOrWhiteSpace(_loadedRelativePath))
        {
            errorMessage = "No static model asset is loaded.";
            return false;
        }

        try
        {
            EditorAssetWriterService.SaveAsset(_loadedRelativePath, _document.Model, EditorAssetSaveSource.StaticModelEditorPanel);
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

    /// <summary>Re-binds the preview texture after the views were drawn.</summary>
    public void RefreshAfterDraw()
    {
        _viewport?.RefreshAfterDraw();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewport?.Dispose();
        _viewport = null;

        if (_document != null)
        {
            ReleaseModel(_document.Model);
        }
    }

    private bool TryApplyMaterial(int meshIndex, int subMeshIndex, Guid materialId, out string error)
    {
        if (!_document.TrySetMaterial(meshIndex, subMeshIndex, materialId, out error))
        {
            SetStatus($"Cannot set the material: {error}");
            return false;
        }

        RefreshPreviewMaterials();
        SetDirty(true);
        SetStatus($"Mesh {meshIndex} material = {DescribeMaterial(materialId)} (not saved)");
        return true;
    }

    private void RefreshInspector()
    {
        if (_meshStack == null)
        {
            return;
        }

        _meshStack.TryRemoveAll();

        if (_document == null)
        {
            _headerText.Text = "[b]Static model[/b]";
            _sourceText.Text = "No static model asset loaded.";
            _viewport?.Clear();
            return;
        }

        StaticModel model = _document.Model;
        _headerText.Text = "[b]" + FTTokenizer.EscapeMarkdown(model.Name ?? string.Empty) + "[/b]";
        _sourceText.Text = FTTokenizer.EscapeMarkdown(_loadedRelativePath ?? string.Empty);

        if (model.Meshes.Count == 0)
        {
            _meshStack.TryAddChild(new MGTextBlock(_window, "This model has no mesh."));
        }

        for (int meshIndex = 0; meshIndex < model.Meshes.Count; meshIndex++)
        {
            StaticModelMesh mesh = model.Meshes[meshIndex];
            _meshStack.TryAddChild(new MGTextBlock(_window,
                $"[b]Mesh {meshIndex}[/b]  {FTTokenizer.EscapeMarkdown(mesh.Name ?? string.Empty)}  [opacity=0.65]{mesh.GetVertices().Count} vertices, {mesh.GetIndices().Count} indices[/opacity]")
            {
                WrapText = true,
            });
            _meshStack.TryAddChild(CreateMaterialRow(meshIndex, StaticModelAssetDocument.WholeMesh, 0));

            for (int subMeshIndex = 0; subMeshIndex < mesh.SubMeshes.Count; subMeshIndex++)
            {
                SubMesh subMesh = mesh.SubMeshes[subMeshIndex];
                _meshStack.TryAddChild(new MGTextBlock(_window,
                    $"Sub-mesh {subMeshIndex}  {FTTokenizer.EscapeMarkdown(subMesh.SlotName ?? string.Empty)}  [opacity=0.65]{subMesh.PrimitiveCount} primitives[/opacity]")
                {
                    Margin = new Thickness(16, 0, 0, 0),
                    WrapText = true,
                });
                _meshStack.TryAddChild(CreateMaterialRow(meshIndex, subMeshIndex, 16));
            }
        }

        if (_viewport != null)
        {
            if (model.Meshes.Count > 0)
            {
                RefreshPreviewMaterials();
            }
            else
            {
                _viewport.Clear();
            }
        }
    }

    /// <summary>
    /// Puts the materials the document names on the previewed model: the model keeps the ones it resolved when it was
    /// first initialised, and a material that was just changed or removed has been dropped by the document.
    /// </summary>
    private void RefreshPreviewMaterials()
    {
        if (_viewport == null || _document == null || _editorRuntime?.AssetContentManager == null)
        {
            return;
        }

        // ShowModel gives the neutral preview material to a mesh that names none, then the rest is resolved from the ids.
        _viewport.ShowModel(_document.Model);
        _document.Model.RefreshResolvedMaterials(_editorRuntime.AssetContentManager);
    }

    private MGElement CreateMaterialRow(int meshIndex, int subMeshIndex, int indent)
    {
        var row = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 6,
            Margin = new Thickness(indent, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        Guid current = _document.GetMaterialAssetId(meshIndex, subMeshIndex);
        var idText = new MGTextBlock(_window, FormatMaterialId(current))
        {
            Opacity = 0.7f,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var selector = new AssetSelector(_window)
        {
            AssetId = current,
            MinWidth = 200,
            Filter = static assetInfo => string.Equals(assetInfo.AssetType, StaticModelAssetDocument.MaterialAssetType, StringComparison.OrdinalIgnoreCase),
        };
        selector.AssetChanged += (_, materialId) =>
        {
            if (TryApplyMaterial(meshIndex, subMeshIndex, materialId, out string _))
            {
                idText.Text = FormatMaterialId(materialId);
            }
        };

        var clearButton = new MGButton(_window, _ =>
        {
            if (TryApplyMaterial(meshIndex, subMeshIndex, Guid.Empty, out string _))
            {
                selector.AssetId = Guid.Empty;
                idText.Text = FormatMaterialId(Guid.Empty);
            }
        })
        {
            PreferredWidth = 60,
        };
        clearButton.SetContent(new MGTextBlock(_window, "None")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.TryAddChild(new MGTextBlock(_window, "Material")
        {
            PreferredWidth = 60,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.TryAddChild(selector);
        row.TryAddChild(clearButton);
        row.TryAddChild(idText);
        return row;
    }

    private static string FormatMaterialId(Guid materialId)
    {
        return materialId == Guid.Empty ? "none" : materialId.ToString("D");
    }

    private static string DescribeMaterial(Guid materialId)
    {
        if (materialId == Guid.Empty)
        {
            return "none";
        }

        AssetInfo info = AssetCatalog.Get(materialId);
        return info != null ? $"{info.Name} ({materialId:D})" : materialId.ToString("D");
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

    private static void ReleaseModel(StaticModel model)
    {
        foreach (StaticModelMesh mesh in model.Meshes)
        {
            mesh.VertexBuffer?.Dispose();
            mesh.IndexBuffer?.Dispose();
        }

        model.Dispose();
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
