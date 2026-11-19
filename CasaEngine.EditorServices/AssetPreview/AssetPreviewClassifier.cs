using System.Text;
using CasaEngine.Framework.Configuration;

namespace CasaEngine.EditorServices.AssetPreview;

/// <summary>
/// Decides how a file is previewed (plan P6). Known media extensions map to a kind; every other extension is Text or
/// Binary by content. An engine asset format (<see cref="Constants.FileNameExtensions"/>) is never previewed: it stays
/// unopened until it gets its own document.
/// </summary>
public static class AssetPreviewClassifier
{
    /// <summary>Number of leading bytes inspected to tell text from binary.</summary>
    public const int SniffLength = 8192;

    private static readonly HashSet<string> EngineAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        Constants.FileNameExtensions.Entity,
        Constants.FileNameExtensions.World,
        Constants.FileNameExtensions.PlayerStartupSettings,
        Constants.FileNameExtensions.GameplayMode,
        Constants.FileNameExtensions.ButtonsMapping,
        Constants.FileNameExtensions.Texture,
        Constants.FileNameExtensions.Sprite,
        Constants.FileNameExtensions.Animation2d,
        Constants.FileNameExtensions.TileMap,
        Constants.FileNameExtensions.TileSet,
        Constants.FileNameExtensions.UIScreen,
        Constants.FileNameExtensions.Screen,
        Constants.FileNameExtensions.Model,
        Constants.FileNameExtensions.StaticModel,
        Constants.FileNameExtensions.Skeleton,
        Constants.FileNameExtensions.SkeletonAnimation,
        Constants.FileNameExtensions.RetargetProfile,
        Constants.FileNameExtensions.Material,
        Constants.FileNameExtensions.Environment,
        Constants.FileNameExtensions.Shader,
        Constants.FileNameExtensions.Particle,
        Constants.FileNameExtensions.Cutscene,
        Constants.FileNameExtensions.Dialogue,
        Constants.FileNameExtensions.Sound,
        Constants.FileNameExtensions.AudioMixer,
    };

    /// <summary>True when <paramref name="extension"/> (with its dot) is an engine asset format.</summary>
    public static bool IsEngineAssetExtension(string extension)
        => !string.IsNullOrEmpty(extension) && EngineAssetExtensions.Contains(extension);

    /// <summary>
    /// Kind of preview for <paramref name="fullPath"/>. Returns <see cref="AssetPreviewKind.None"/> for an engine asset
    /// format, and for any other extension whose file does not exist or cannot be read.
    /// </summary>
    public static AssetPreviewKind Classify(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return AssetPreviewKind.None;
        }

        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (IsEngineAssetExtension(extension))
        {
            return AssetPreviewKind.None;
        }

        switch (extension)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".bmp":
            case ".gif":
            case ".tga":
                return File.Exists(fullPath) ? AssetPreviewKind.Image : AssetPreviewKind.None;
            case ".dds":
                return File.Exists(fullPath) ? AssetPreviewKind.CubeMap : AssetPreviewKind.None;
            case ".wav":
            case ".ogg":
                return File.Exists(fullPath) ? AssetPreviewKind.Audio : AssetPreviewKind.None;
            case ".fnt":
                return File.Exists(fullPath) ? AssetPreviewKind.BitmapFont : AssetPreviewKind.None;
            case ".gltf":
            case ".glb":
                return File.Exists(fullPath) ? AssetPreviewKind.Model : AssetPreviewKind.None;
        }

        return TryReadHead(fullPath, out byte[] head, out int count)
            ? (LooksLikeText(head, count) ? AssetPreviewKind.Text : AssetPreviewKind.Binary)
            : AssetPreviewKind.None;
    }

    /// <summary>True when the first <paramref name="count"/> bytes decode as UTF-8 and hold no NUL byte.</summary>
    public static bool LooksLikeText(byte[] head, int count)
    {
        ArgumentNullException.ThrowIfNull(head);

        for (int i = 0; i < count; i++)
        {
            if (head[i] == 0)
            {
                return false;
            }
        }

        try
        {
            // flush: false lets a multi-byte sequence cut by the end of the window pass.
            new UTF8Encoding(false, true).GetDecoder().GetCharCount(head, 0, count, flush: false);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    internal static bool TryReadHead(string fullPath, out byte[] head, out int count)
    {
        head = new byte[SniffLength];
        count = 0;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            while (count < head.Length)
            {
                int read = stream.Read(head, count, head.Length - count);
                if (read <= 0)
                {
                    break;
                }

                count += read;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
