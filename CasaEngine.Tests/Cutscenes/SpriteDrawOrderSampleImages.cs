using Microsoft.Xna.Framework;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The synthetic image of Projects/SpriteDrawOrderSample: three translucent discs (red, green, blue) side by side. Every number
/// is an integer, so the pixels are the same on every machine; a data test decodes the committed PNG file and compares its pixels
/// with this output.
/// </summary>
internal static class SpriteDrawOrderSampleImages
{
    /// <summary>Side of each disc, in pixels.</summary>
    public const int DiscSize = 48;

    /// <summary>The number of discs on the sheet.</summary>
    public const int DiscCount = 3;

    /// <summary>Width of the sheet.</summary>
    public const int SheetWidth = DiscCount * DiscSize;

    /// <summary>Alpha of the pixels inside a disc, so the stacking stays readable where discs overlap.</summary>
    public const int DiscAlpha = 200;

    /// <summary>The colour of each disc, left to right on the sheet: red, green, blue.</summary>
    public static Color DiscColor(int disc) => disc switch
    {
        0 => new Color(230, 40, 40, DiscAlpha),
        1 => new Color(40, 200, 60, DiscAlpha),
        _ => new Color(50, 70, 230, DiscAlpha),
    };

    /// <summary>A flat coloured disc of radius 24 pixels in each 48 pixel cell, transparent black outside.</summary>
    public static Color SheetPixel(int x, int y)
    {
        var disc = x / DiscSize;
        var dx = 2 * (x % DiscSize) - (DiscSize - 1);
        var dy = 2 * y - (DiscSize - 1);
        return dx * dx + dy * dy <= DiscSize * DiscSize ? DiscColor(disc) : new Color(0, 0, 0, 0);
    }

    public static byte[] BuildSheetPng() => SpriteCutsceneSampleImages.WritePng(SheetWidth, DiscSize, SheetPixel);
}
