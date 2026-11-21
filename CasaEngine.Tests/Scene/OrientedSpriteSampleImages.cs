using CasaEngine.Tests.Cutscenes;
using Microsoft.Xna.Framework;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The synthetic images of Projects/OrientedSpriteSample: a far picture, a near picture with a cut-out, the picture of the
/// sprite that is turned in the world, and the texture of the box. Every number is an integer, so the pixels are the same on
/// every machine; a data test decodes the committed PNG files and compares their pixels with this output.
/// </summary>
internal static class OrientedSpriteSampleImages
{
    /// <summary>Resolution of the sample (the project file asks for it): the frame the scale of the sprites is computed for.</summary>
    public const int FrameWidth = 640;
    public const int FrameHeight = 480;

    /// <summary>Size of the far and near pictures: larger than the frame in both directions.</summary>
    public const int PictureWidth = 960;
    public const int PictureHeight = 720;

    /// <summary>Side of the cells of the grid of the far picture.</summary>
    public const int GridCell = 60;

    /// <summary>The cut-out of the near picture, in picture pixels: transparent inside, opaque around.</summary>
    public const int CutoutLeft = 240;
    public const int CutoutTop = 200;
    public const int CutoutWidth = 480;
    public const int CutoutHeight = 320;

    public const int TurnedSize = 64;
    public const int BoxTextureSize = 8;

    private const int CornerMarkSize = 40;
    private const int CornerMarkInset = 20;
    private const int RimWidth = 4;
    private const int NearCell = 40;

    private static readonly Color White = new(255, 255, 255);

    public static Color FarPixel(int x, int y)
    {
        int right = PictureWidth - CornerMarkInset - CornerMarkSize;
        int bottom = PictureHeight - CornerMarkInset - CornerMarkSize;
        bool inLeftColumn = x >= CornerMarkInset && x < CornerMarkInset + CornerMarkSize;
        bool inRightColumn = x >= right && x < right + CornerMarkSize;
        bool inTopRow = y >= CornerMarkInset && y < CornerMarkInset + CornerMarkSize;
        bool inBottomRow = y >= bottom && y < bottom + CornerMarkSize;

        if (inLeftColumn && inTopRow)
        {
            return new Color(220, 60, 60);
        }

        if (inRightColumn && inTopRow)
        {
            return new Color(60, 200, 80);
        }

        if (inLeftColumn && inBottomRow)
        {
            return new Color(60, 100, 230);
        }

        if (inRightColumn && inBottomRow)
        {
            return new Color(240, 210, 50);
        }

        // The cross through the centre of the picture, which is the centre of the frame while the picture is not shifted.
        int centreX = PictureWidth / 2;
        int centreY = PictureHeight / 2;
        if ((x >= centreX - 2 && x < centreX + 2) || (y >= centreY - 2 && y < centreY + 2))
        {
            return White;
        }

        if (x % (4 * GridCell) < RimWidth || y % (4 * GridCell) < RimWidth)
        {
            return new Color(255, 200, 120);
        }

        if (x % GridCell < 2 || y % GridCell < 2)
        {
            return new Color(150, 175, 215);
        }

        bool light = (x / GridCell + y / GridCell) % 2 == 1;
        return light ? new Color(50, 75, 120) : new Color(40, 60, 100);
    }

    public static Color NearPixel(int x, int y)
    {
        bool inCutout = x >= CutoutLeft && x < CutoutLeft + CutoutWidth && y >= CutoutTop && y < CutoutTop + CutoutHeight;
        if (inCutout)
        {
            return new Color(0, 0, 0, 0);
        }

        bool nearCutout = x >= CutoutLeft - RimWidth && x < CutoutLeft + CutoutWidth + RimWidth
            && y >= CutoutTop - RimWidth && y < CutoutTop + CutoutHeight + RimWidth;
        bool onEdge = x < RimWidth || y < RimWidth || x >= PictureWidth - RimWidth || y >= PictureHeight - RimWidth;
        if (nearCutout || onEdge)
        {
            return White;
        }

        bool light = (x / NearCell + y / NearCell) % 2 == 1;
        return light ? new Color(170, 90, 55) : new Color(150, 70, 40);
    }

    /// <summary>
    /// A teal tile with a white border, a red square in its top-left corner and a yellow arrow that points up, so that a
    /// turn of the tile, a mirror and a view from the side can all be told.
    /// </summary>
    public static Color TurnedPixel(int x, int y)
    {
        bool border = x < 3 || y < 3 || x >= TurnedSize - 3 || y >= TurnedSize - 3;
        if (border)
        {
            return White;
        }

        if (x >= 6 && x < 18 && y >= 6 && y < 18)
        {
            return new Color(220, 60, 60);
        }

        // Half width of the arrow in doubled pixels, 8/7 of the distance below the tip (31, 12).
        bool inArrow = y >= 12 && y < 52 && Math.Abs(2 * x - (TurnedSize - 1)) * 7 <= (y - 12) * 8;
        return inArrow ? new Color(250, 220, 40) : new Color(30, 150, 170);
    }

    /// <summary>Orange with a darker edge, so that the faces of the box can be told apart.</summary>
    public static Color BoxPixel(int x, int y)
    {
        bool edge = x == 0 || y == 0 || x == BoxTextureSize - 1 || y == BoxTextureSize - 1;
        return edge ? new Color(160, 70, 10) : new Color(240, 120, 30);
    }

    public static byte[] BuildFarPng() => SpriteCutsceneSampleImages.WritePng(PictureWidth, PictureHeight, FarPixel);

    public static byte[] BuildNearPng() => SpriteCutsceneSampleImages.WritePng(PictureWidth, PictureHeight, NearPixel);

    public static byte[] BuildTurnedPng() => SpriteCutsceneSampleImages.WritePng(TurnedSize, TurnedSize, TurnedPixel);

    public static byte[] BuildBoxPng() => SpriteCutsceneSampleImages.WritePng(BoxTextureSize, BoxTextureSize, BoxPixel);
}
