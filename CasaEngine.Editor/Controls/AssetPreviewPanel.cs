using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices.AssetPreview;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Rendering.Models;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Text;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Read-only document that previews a catalogued file which has no editing document (plan P6): the file name, the lines
/// read by <see cref="AssetPreviewReader"/>, and a preview area chosen by <see cref="AssetPreviewKind"/> (image, the six
/// faces of a cube map, play and stop for audio, the pages of a bitmap font, a 3D static model, the first lines of a text
/// file, or a grayscale map for a binary file). Nothing is written to the file.
/// </summary>
/// <remarks>
/// The GPU textures and the model of the preview are created when the content is built and released by
/// <see cref="Dispose"/>, which the editor calls when the tab closes. A preview that cannot be built (unsupported pixel
/// format, truncated file) leaves the information lines and says why in the status line.
/// </remarks>
internal sealed class AssetPreviewPanel : IDisposable
{
    private const int FacePreviewSize = 160;
    private const int MaxPageImageHeight = 512;

    private static readonly string[] CubeFaceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

    private static readonly CubeMapFace[] CubeFaces =
    {
        CubeMapFace.PositiveX,
        CubeMapFace.NegativeX,
        CubeMapFace.PositiveY,
        CubeMapFace.NegativeY,
        CubeMapFace.PositiveZ,
        CubeMapFace.NegativeZ,
    };

    private readonly MGWindow _window;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly List<Texture2D> _textures = new();
    private readonly FontStashSharpTextEngine _textEngine;

    private AssetPreviewInfo _info;
    private MGDockPanel _root;
    private MGTextBlock _statusText;
    private ModelPreviewViewport _modelViewport;
    private StaticModel _model;
    private BitmapFont _sampleFont;
    private string _sampleFontFamily;
    private AudioVoiceHandle _previewVoice = AudioVoiceHandle.None;
    private MusicTrackHandle _previewTrack = MusicTrackHandle.None;
    private bool _disposed;

    /// <summary>Creates the panel for <paramref name="info"/>; the content is built by <see cref="CreateContent"/>.</summary>
    /// <param name="textEngine">The text engine of the editor window, on which a bitmap font is registered for its sample text; null shows no sample.</param>
    public AssetPreviewPanel(MGWindow window, GraphicsDevice graphicsDevice, HostedEditorGameAdapter editorRuntime, AssetPreviewInfo info, FontStashSharpTextEngine textEngine = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(info);

        _window = window;
        _graphicsDevice = graphicsDevice;
        _editorRuntime = editorRuntime;
        _info = info;
        _textEngine = textEngine;
    }

    /// <summary>What the panel shows.</summary>
    public AssetPreviewInfo Info => _info;

    /// <summary>Text of the status line (why a preview is missing, or what the audio preview does).</summary>
    public string StatusMessage => _statusText?.Text ?? string.Empty;

    /// <summary>Builds the content on first call; returns the same element afterwards.</summary>
    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        var header = new MGTextBlock(_window, "[b]" + FTTokenizer.EscapeMarkdown(_info.FileName) + "[/b]  [opacity=0.65]read-only preview[/opacity]")
        {
            Margin = new Thickness(8, 6, 8, 4),
            WrapText = true,
        };

        var lines = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 1,
            Margin = new Thickness(8, 0, 8, 4),
        };
        foreach (AssetPreviewLine line in _info.Lines)
        {
            lines.TryAddChild(new MGTextBlock(_window, "[opacity=0.65]" + FTTokenizer.EscapeMarkdown(line.Label) + ":[/opacity]  " + FTTokenizer.EscapeMarkdown(line.Value))
            {
                WrapText = true,
            });
        }

        _statusText = new MGTextBlock(_window, string.Empty)
        {
            Margin = new Thickness(8, 0, 8, 6),
            Opacity = 0.75f,
            WrapText = true,
        };

        _root = new MGDockPanel(_window);
        _root.TryAddChild(header, Dock.Top);
        _root.TryAddChild(lines, Dock.Top);
        _root.TryAddChild(_statusText, Dock.Top);

        MGElement preview = BuildPreview();
        if (preview != null)
        {
            _root.TryAddChild(preview, Dock.Top);
        }

        return _root;
    }

    /// <summary>Re-binds the model view's texture after the views were drawn; no-op for the other kinds.</summary>
    public void RefreshAfterDraw()
    {
        _modelViewport?.RefreshAfterDraw();
    }

    /// <summary>Starts the audio preview on the Editor bus. Does nothing for a file that is not audio.</summary>
    public void PlayPreview()
    {
        if (_info.Kind != AssetPreviewKind.Audio)
        {
            return;
        }

        AudioService audioService = _editorRuntime?.AudioSystemComponent?.Service;
        if (audioService == null)
        {
            SetStatus("No audio service available for the preview.");
            return;
        }

        AssetInfo assetInfo = FindCatalogEntry(_info.FullPath);
        if (assetInfo == null)
        {
            SetStatus("The file is not declared in the asset catalog, so the engine cannot play it.");
            return;
        }

        StopPreview();

        // A raw wav or ogg is not a SoundAsset: a transient one, never saved, names the catalogued file. The Editor bus keeps
        // the game mix from silencing the preview, and priority 0 means it never steals a game voice.
        bool streamed = _info.Extension == ".ogg";
        var transient = new SoundAsset
        {
            Name = _info.FileName,
            AudioFileAssetId = assetInfo.Id,
            IsStreaming = streamed,
            BusName = AudioBusNames.Editor,
        };

        if (streamed)
        {
            _previewTrack = audioService.Music.Play(transient);
            SetStatus(_previewTrack.IsValid ? "Playing (streamed)" : "The stream could not be started.");
            return;
        }

        _previewVoice = audioService.PlaySound(transient, new SoundPlaybackOverrides(busName: AudioBusNames.Editor) { Priority = 0 });
        SetStatus(_previewVoice.IsValid ? "Playing" : "The sound could not be played.");
    }

    /// <summary>Stops the audio preview.</summary>
    public void StopPreview()
    {
        AudioService audioService = _editorRuntime?.AudioSystemComponent?.Service;
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

    /// <summary>Stops the audio and releases the textures, the model and its view.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopPreview();

        _modelViewport?.Dispose();
        _modelViewport = null;
        ReleaseModel();
        ReleaseSampleFont();

        foreach (Texture2D texture in _textures)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }

    private MGElement BuildPreview()
    {
        if (_info.Problem != null)
        {
            SetStatus(_info.Problem);
        }

        switch (_info.Kind)
        {
            case AssetPreviewKind.Image:
                return BuildImagePreview();
            case AssetPreviewKind.CubeMap:
                return BuildCubeMapPreview();
            case AssetPreviewKind.Audio:
                return BuildAudioPreview();
            case AssetPreviewKind.BitmapFont:
                return BuildBitmapFontPreview();
            case AssetPreviewKind.Model:
                return BuildModelPreview();
            case AssetPreviewKind.Text:
                return BuildTextPreview();
            case AssetPreviewKind.Binary:
                return BuildBinaryPreview();
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------ image

    private MGElement BuildImagePreview()
    {
        Texture2D texture = TryLoadImage(_info.FullPath, out string error);
        if (texture == null)
        {
            SetStatus(error);
            return null;
        }

        return CreateFitImage(texture, minHeight: 200);
    }

    private Texture2D TryLoadImage(string fullPath, out string error)
    {
        error = null;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Texture2D texture = Texture2D.FromStream(_graphicsDevice, stream);
            _textures.Add(texture);
            return texture;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            error = $"The image cannot be shown: '{fullPath}' could not be decoded ({exception.Message}).";
            Logs.WriteWarning(error);
            return null;
        }
    }

    private MGImage CreateFitImage(Texture2D texture, int minHeight)
    {
        return new MGImage(_window, new MGTextureData(EditorIcons.AsImage(texture)!), Stretch: Stretch.Uniform)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(8, 0, 8, 8),
            MinHeight = minHeight,
            IsHitTestVisible = false,
        };
    }

    // ---------------------------------------------------------------- cube map

    private MGElement BuildCubeMapPreview()
    {
        if (!_info.IsCubeMap)
        {
            SetStatus("The DDS file is not a cube map, so there are no six faces to show.");
            return null;
        }

        XnaTextureCube cube;
        try
        {
            cube = TextureCubeLoader.LoadTextureCube(_info.FullPath, _graphicsDevice);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            SetStatus($"The faces cannot be shown: {exception.Message}");
            Logs.WriteWarning($"Cube map preview of '{_info.FullPath}': {exception.Message}");
            return null;
        }

        var grid = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            Margin = new Thickness(8, 0, 8, 8),
        };

        try
        {
            var row = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 8 };
            for (int faceIndex = 0; faceIndex < CubeFaces.Length; faceIndex++)
            {
                if (faceIndex == 3)
                {
                    grid.TryAddChild(row);
                    row = new MGStackPanel(_window, Orientation.Horizontal) { Spacing = 8 };
                }

                Texture2D faceTexture = CreateFaceTexture(cube, CubeFaces[faceIndex]);
                _textures.Add(faceTexture);

                var cell = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 2 };
                cell.TryAddChild(new MGTextBlock(_window, "[opacity=0.65]" + CubeFaceNames[faceIndex] + "[/opacity]"));
                cell.TryAddChild(new MGImage(_window, new MGTextureData(EditorIcons.AsImage(faceTexture)!), Stretch: Stretch.Uniform)
                {
                    PreferredWidth = FacePreviewSize,
                    PreferredHeight = FacePreviewSize,
                    IsHitTestVisible = false,
                });
                row.TryAddChild(cell);
            }

            grid.TryAddChild(row);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            SetStatus($"The faces cannot be shown: {exception.Message}");
            Logs.WriteWarning($"Cube map preview of '{_info.FullPath}': {exception.Message}");
            return null;
        }
        finally
        {
            cube.Dispose();
        }

        var scroll = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scroll.SetContent(grid);
        return scroll;
    }

    /// <summary>Copies one face into a 2D texture of the same surface format.</summary>
    private Texture2D CreateFaceTexture(XnaTextureCube cube, CubeMapFace face)
    {
        int size = cube.Size;
        int byteCount = GetSurfaceByteCount(cube.Format, size);
        var data = new byte[byteCount];
        cube.GetData(face, data);

        var texture = new Texture2D(_graphicsDevice, size, size, false, cube.Format);
        texture.SetData(data);
        return texture;
    }

    private static int GetSurfaceByteCount(SurfaceFormat format, int size)
    {
        if (format is SurfaceFormat.Dxt1 or SurfaceFormat.Dxt1SRgb or SurfaceFormat.Dxt1a)
        {
            int blocks = (size + 3) / 4;
            return blocks * blocks * 8;
        }

        if (format is SurfaceFormat.Dxt3 or SurfaceFormat.Dxt3SRgb or SurfaceFormat.Dxt5 or SurfaceFormat.Dxt5SRgb)
        {
            int blocks = (size + 3) / 4;
            return blocks * blocks * 16;
        }

        int bytesPerPixel = format switch
        {
            SurfaceFormat.Color or SurfaceFormat.ColorSRgb or SurfaceFormat.Bgra32 or SurfaceFormat.Bgr32
                or SurfaceFormat.Rgba1010102 or SurfaceFormat.Rg32 or SurfaceFormat.Single => 4,
            SurfaceFormat.Bgr565 or SurfaceFormat.Bgra5551 or SurfaceFormat.Bgra4444 or SurfaceFormat.NormalizedByte2
                or SurfaceFormat.HalfSingle => 2,
            SurfaceFormat.Alpha8 => 1,
            SurfaceFormat.HalfVector4 or SurfaceFormat.Rgba64 or SurfaceFormat.NormalizedByte4 or SurfaceFormat.HalfVector2 => 8,
            SurfaceFormat.Vector4 => 16,
            _ => throw new NotSupportedException($"the surface format {format} is not supported by the preview"),
        };
        return size * size * bytesPerPixel;
    }

    // ------------------------------------------------------------------- audio

    private MGElement BuildAudioPreview()
    {
        var toolbar = new MGStackPanel(_window, Orientation.Horizontal)
        {
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8),
            VerticalAlignment = VerticalAlignment.Top,
        };
        toolbar.TryAddChild(CreateButton("Play", PlayPreview));
        toolbar.TryAddChild(CreateButton("Stop", StopPreview));
        return toolbar;
    }

    private MGButton CreateButton(string label, Action onClick)
    {
        var button = new MGButton(_window, _ => onClick())
        {
            PreferredWidth = 72,
            PreferredHeight = 26,
        };
        button.SetContent(new MGTextBlock(_window, label)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return button;
    }

    private static AssetInfo FindCatalogEntry(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(EngineEnvironment.ProjectPath))
        {
            return null;
        }

        string relativePath = Path.GetRelativePath(EngineEnvironment.ProjectPath, fullPath);
        return AssetCatalog.GetByFileName(relativePath)
               ?? AssetCatalog.GetByFileName(relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    // ------------------------------------------------------------- bitmap font

    private MGElement BuildBitmapFontPreview()
    {
        if (_info.PageFiles.Count == 0)
        {
            return null;
        }

        MGTextBlock sample = TryCreateSampleText();

        string folder = Path.GetDirectoryName(_info.FullPath) ?? string.Empty;
        var pages = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 8,
            Margin = new Thickness(8, 0, 8, 8),
        };

        var missing = new List<string>();
        foreach (string pageFile in _info.PageFiles)
        {
            string pagePath = Path.GetFullPath(Path.Combine(folder, pageFile));
            Texture2D texture = File.Exists(pagePath) ? TryLoadImage(pagePath, out _) : null;
            if (texture == null)
            {
                missing.Add(pageFile);
                continue;
            }

            pages.TryAddChild(new MGTextBlock(_window, "[opacity=0.65]" + FTTokenizer.EscapeMarkdown(pageFile) + "  "
                + texture.Width.ToString(CultureInfo.InvariantCulture) + " x " + texture.Height.ToString(CultureInfo.InvariantCulture) + "[/opacity]"));
            pages.TryAddChild(new MGImage(_window, new MGTextureData(EditorIcons.AsImage(texture)!), Stretch: Stretch.Uniform)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                PreferredWidth = texture.Width,
                PreferredHeight = Math.Min(texture.Height, MaxPageImageHeight),
                IsHitTestVisible = false,
            });
        }

        if (missing.Count > 0)
        {
            SetStatus("Page image not shown: " + string.Join(", ", missing) + " (missing or not decodable, looked up next to the descriptor).");
        }

        if (sample != null)
        {
            pages.TryInsertChild(0, sample);
        }

        var scroll = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scroll.SetContent(pages);
        return scroll;
    }

    /// <summary>
    /// Loads the font with the engine loader (the panel owns it, outside the asset cache) and registers it on the editor text
    /// engine under a family no real font uses, then returns a text block drawn with it. On failure it says why in the status
    /// line and returns null, so the page images are still shown.
    /// </summary>
    private MGTextBlock TryCreateSampleText()
    {
        if (_textEngine == null || _editorRuntime == null)
        {
            SetStatus("No sample text: the editor text engine is not available.");
            return null;
        }

        try
        {
            _sampleFont = (BitmapFont)new BitmapFontLoader().LoadAsset(_info.FullPath, _editorRuntime.AssetContentManager);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or KeyNotFoundException
                                              or FormatException or ArgumentException or NotSupportedException)
        {
            SetStatus($"No sample text: the font cannot be loaded ({exception.Message})");
            Logs.WriteWarning($"Bitmap font preview of '{_info.FullPath}': {exception.Message}");
            return null;
        }

        _sampleFontFamily = "assetpreview-" + Guid.NewGuid().ToString("N");
        _textEngine.AddStaticFont(_sampleFontFamily, CustomFontStyles.Normal, _sampleFont.Font);

        var text = new MGTextBlock(_window, "The quick brown fox jumps over\nthe lazy dog 0123456789")
        {
            Margin = new Thickness(0, 0, 0, 4),
        };
        text.TrySetFont(_sampleFontFamily, 36);
        return text;
    }

    private void ReleaseSampleFont()
    {
        if (_sampleFontFamily != null)
        {
            _textEngine?.RemoveStaticFont(_sampleFontFamily, CustomFontStyles.Normal);
            _sampleFontFamily = null;
        }

        // Disposing gives back the page texture handles the loader acquired.
        _sampleFont?.Dispose();
        _sampleFont = null;
    }

    // ------------------------------------------------------------------- model

    private MGElement BuildModelPreview()
    {
        if (_editorRuntime == null)
        {
            SetStatus("The 3D preview needs the editor runtime, which is not available.");
            return null;
        }

        try
        {
            _model = new GltfStaticModelReader().Read(_info.FullPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            SetStatus($"The model cannot be read: {exception.Message}");
            Logs.WriteWarning($"Model preview of '{_info.FullPath}': {exception.Message}");
            return null;
        }

        if (_model.Meshes.Count == 0)
        {
            SetStatus("The model has no mesh to show, or it could not be read (see the logs).");
            _model = null;
            return null;
        }

        _modelViewport = new ModelPreviewViewport(_window, _graphicsDevice, _editorRuntime, minHeight: 320);
        MGElement content = _modelViewport.CreateContent();
        _modelViewport.ShowModel(_model);
        return content;
    }

    private void ReleaseModel()
    {
        if (_model == null)
        {
            return;
        }

        foreach (StaticModelMesh mesh in _model.Meshes)
        {
            mesh.VertexBuffer?.Dispose();
            mesh.IndexBuffer?.Dispose();
        }

        _model.Dispose();
        _model = null;
    }

    // -------------------------------------------------------------------- text

    private MGElement BuildTextPreview()
    {
        // FTTokenizer.EscapeMarkdown keeps every '[' of the file text from being read as MGUI markup (as LogsPanel does).
        var text = new MGTextBlock(_window, FTTokenizer.EscapeMarkdown(_info.TextPreview))
        {
            Margin = new Thickness(8, 0, 8, 8),
            WrapText = false,
        };

        var scroll = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scroll.SetContent(text);
        return scroll;
    }

    // ------------------------------------------------------------------ binary

    private MGElement BuildBinaryPreview()
    {
        int side = _info.SquareMapSide;
        if (side == 0)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(_info.FullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus($"The map cannot be shown: {exception.Message}");
            return null;
        }

        if (bytes.Length != (long)side * side)
        {
            SetStatus("The map cannot be shown: the file changed size since it was read.");
            return null;
        }

        byte[] gray = AssetPreviewGrayscale.Downsample(bytes, side, AssetPreviewGrayscale.MaxPreviewSide, out int outputSide);
        var pixels = new Color[gray.Length];
        for (int i = 0; i < gray.Length; i++)
        {
            byte value = gray[i];
            pixels[i] = new Color(value, value, value, (byte)255);
        }

        var texture = new Texture2D(_graphicsDevice, outputSide, outputSide, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        _textures.Add(texture);

        SetStatus(side > outputSide
            ? $"Grayscale preview of the {side} x {side} map, sampled down to {outputSide} x {outputSide}."
            : $"Grayscale preview of the {side} x {side} map.");
        return CreateFitImage(texture, minHeight: 256);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.Text = FTTokenizer.EscapeMarkdown(message);
        }
    }
}
