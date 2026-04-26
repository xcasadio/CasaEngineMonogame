using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Styling;

public readonly record struct EditorBadgeColors(Color BackgroundColor, Color BorderColor, Color ForegroundColor);

public static class EditorThemePalette
{
    public static readonly Color ToolbarBackground = new(26, 30, 38);
    public static readonly Color TreeBackground = new(22, 25, 31);
    public static readonly Color ContentBackground = new(18, 21, 28);
    public static readonly Color PanelBorder = new(62, 72, 88);
    public static readonly Color AccentSelection = new(58, 110, 182, 185);
    public static readonly Color DropHighlight = new(70, 130, 180, 96);

    public static readonly Color GridItemSelectedBackground = new(52, 96, 156, 180);
    public static readonly Color GridItemHoverBackground = new(50, 50, 58, 180);
    public static readonly Color GridItemPreviewBackground = new(18, 18, 22);

    public static readonly Color PreviewClearColor = new(20, 22, 28);
    public static readonly Color PreviewSurfaceBackground = new(18, 18, 22);
    public static readonly Color PreviewSurfaceBorder = new(74, 74, 82);

    public static readonly Color OverlayPopupBackground = new(24, 28, 36);
    public static readonly Color InlineRenameBorder = new(82, 132, 204);
    public static readonly Color InlineRenameInvalidBorder = Color.IndianRed;

    public const float PrimaryHeaderOpacity = 0.9f;
    public const float SecondaryHeadingOpacity = 0.78f;
    public const float SecondaryTextOpacity = 0.72f;
    public const float SectionHeaderOpacity = 0.8f;
    public const float SectionLabelOpacity = 0.75f;

    public static EditorBadgeColors OverrideBadge { get; } = new(
        new Color(94, 61, 20),
        new Color(201, 145, 53),
        new Color(255, 241, 210));

    public static EditorBadgeColors InheritedBadge { get; } = new(
        new Color(33, 52, 74),
        new Color(98, 143, 188),
        new Color(223, 238, 255));

    public static EditorBadgeColors DefaultBadge { get; } = new(
        new Color(42, 42, 48),
        new Color(92, 92, 104),
        new Color(226, 226, 230));
}