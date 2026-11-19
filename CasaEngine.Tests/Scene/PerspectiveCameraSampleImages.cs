using CasaEngine.Tests.Cutscenes;
using Microsoft.Xna.Framework;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The synthetic images of Projects/PerspectiveCameraSample: a backdrop twice as wide as the picture the camera shows, and
/// the texture of the box that stands on it. Every number is an integer, so the pixels are the same on every machine; a data
/// test decodes the committed PNG files and compares their pixels with this output.
/// </summary>
internal static class PerspectiveCameraSampleImages
{
    public const int BackdropWidth = 640;
    public const int BackdropHeight = 240;

    /// <summary>Side of the squares of the checker.</summary>
    public const int CellSize = 40;

    /// <summary>Width of each of the four coloured columns, which tell how far the picture has scrolled.</summary>
    public const int ColumnWidth = 160;

    /// <summary>The yellow square the box stands on, in backdrop pixels.</summary>
    public const int MarkLeft = 400;
    public const int MarkTop = 140;
    public const int MarkSize = 40;

    public const int BoxTextureSize = 8;

    private static Color ColumnColor(int column) => column switch
    {
        0 => new Color(60, 90, 150),
        1 => new Color(60, 140, 110),
        2 => new Color(150, 110, 60),
        _ => new Color(130, 70, 140),
    };

    public static Color BackdropPixel(int x, int y)
    {
        if (x >= MarkLeft && x < MarkLeft + MarkSize && y >= MarkTop && y < MarkTop + MarkSize)
        {
            bool border = x < MarkLeft + 2 || x >= MarkLeft + MarkSize - 2 || y < MarkTop + 2 || y >= MarkTop + MarkSize - 2;
            return border ? new Color(255, 255, 255) : new Color(250, 220, 40);
        }

        Color color = ColumnColor(x / ColumnWidth);
        bool light = (x / CellSize + y / CellSize) % 2 == 1;
        return light ? new Color(color.R + 30, color.G + 30, color.B + 30) : color;
    }

    /// <summary>Orange with a darker edge, so that the faces of the box can be told apart.</summary>
    public static Color BoxPixel(int x, int y)
    {
        bool edge = x == 0 || y == 0 || x == BoxTextureSize - 1 || y == BoxTextureSize - 1;
        return edge ? new Color(160, 70, 10) : new Color(240, 120, 30);
    }

    public static byte[] BuildBackdropPng() => SpriteCutsceneSampleImages.WritePng(BackdropWidth, BackdropHeight, BackdropPixel);

    public static byte[] BuildBoxPng() => SpriteCutsceneSampleImages.WritePng(BoxTextureSize, BoxTextureSize, BoxPixel);
}
