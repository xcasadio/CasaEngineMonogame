using System;
using System.IO;
using CasaEngine.Editor.ContentBrowser.Models;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.ContentBrowser;

public static class ContentItemDisplay
{
    public static Texture2D? GetIcon(ContentBrowserConfig? config, ContentItemType type)
    {
        if (config != null && config.CustomIcons.TryGetValue(type, out var customIcon) && customIcon != null)
        {
            return customIcon;
        }

        return type switch
        {
            ContentItemType.Folder => EditorIcons.Folder,
            ContentItemType.Texture => EditorIcons.Image,
            ContentItemType.Model => EditorIcons.Box,
            ContentItemType.Sound => EditorIcons.Volume,
            ContentItemType.Script => EditorIcons.FileCode,
            ContentItemType.Scene => EditorIcons.Clapperboard,
            ContentItemType.Shader => EditorIcons.Settings,
            ContentItemType.Font => EditorIcons.Square,
            ContentItemType.Material => EditorIcons.Palette,
            ContentItemType.Particle => EditorIcons.Sliders ?? EditorIcons.Cone ?? EditorIcons.Settings,
            ContentItemType.Prefab => EditorIcons.Package,
            ContentItemType.Animation => EditorIcons.Clapperboard,
            ContentItemType.World => EditorIcons.Layers,
            _ => EditorIcons.FilePlus,
        };
    }

    public static string GetTypeLabel(ContentItem item)
    {
        if (item.IsDirectory)
        {
            return "Folder";
        }

        return item.Type switch
        {
            ContentItemType.Texture => "Texture",
            ContentItemType.Model => "Model",
            ContentItemType.Sound => "Sound",
            ContentItemType.Script => "Script",
            ContentItemType.Scene => "Scene",
            ContentItemType.Shader => "Shader",
            ContentItemType.Font => "Font",
            ContentItemType.Material => "Material",
            ContentItemType.Particle => "Particle",
            ContentItemType.Prefab => "Prefab",
            ContentItemType.Animation => "Animation",
            ContentItemType.World => "World",
            _ => string.IsNullOrWhiteSpace(item.Extension) ? "Unknown" : item.Extension.TrimStart('.').ToUpperInvariant(),
        };
    }

    public static string FormatSize(long size)
    {
        const double kilo = 1024.0;
        const double mega = kilo * 1024.0;
        const double giga = mega * 1024.0;

        if (size >= giga)
        {
            return $"{size / giga:0.0} GB";
        }

        if (size >= mega)
        {
            return $"{size / mega:0.0} MB";
        }

        if (size >= kilo)
        {
            return $"{size / kilo:0.0} KB";
        }

        return $"{size} B";
    }

    public static string GetRelativePath(string rootPath, ContentItem item)
    {
        if (!string.IsNullOrWhiteSpace(rootPath)
            && item.FullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(rootPath, item.FullPath);
        }

        return item.FullPath;
    }
}