using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor;

/// <summary>
/// Holds preloaded <see cref="Texture2D"/> references for the white PNG icon set.
/// Call <see cref="Load"/> once from <c>Game1.LoadContent()</c> before any panel
/// creates its UI.
/// </summary>
public static class EditorIcons
{
    // ── toolbar ────────────────────────────────────────────────────────────
    public static Texture2D? Save       { get; private set; }
    public static Texture2D? SaveAll    { get; private set; }
    public static Texture2D? FolderOpen { get; private set; }

    // ── gizmo / transform ─────────────────────────────────────────────────
    public static Texture2D? Move       { get; private set; }
    public static Texture2D? Rotate     { get; private set; }
    public static Texture2D? Scale      { get; private set; }
    public static Texture2D? Focus      { get; private set; }
    public static Texture2D? Maximize   { get; private set; }
    public static Texture2D? Magnet     { get; private set; }

    // ── playback ──────────────────────────────────────────────────────────
    public static Texture2D? Play       { get; private set; }
    public static Texture2D? Pause      { get; private set; }
    public static Texture2D? Redo       { get; private set; }
    public static Texture2D? Undo       { get; private set; }
    public static Texture2D? RefreshCw  { get; private set; }

    // ── edit ──────────────────────────────────────────────────────────────
    public static Texture2D? FilePlus   { get; private set; }
    public static Texture2D? Trash      { get; private set; }
    public static Texture2D? Pencil     { get; private set; }
    public static Texture2D? Copy       { get; private set; }
    public static Texture2D? Paste      { get; private set; }
    public static Texture2D? Scissors   { get; private set; }

    // ── misc ──────────────────────────────────────────────────────────────
    public static Texture2D? Eye        { get; private set; }
    public static Texture2D? EyeOff     { get; private set; }
    public static Texture2D? Settings   { get; private set; }
    public static Texture2D? Search     { get; private set; }
    public static Texture2D? Package    { get; private set; }
    public static Texture2D? Layers     { get; private set; }
    public static Texture2D? Folder     { get; private set; }
    public static Texture2D? Image      { get; private set; }
    public static Texture2D? Square     { get; private set; }
    public static Texture2D? Box        { get; private set; }
    public static Texture2D? Close      { get; private set; }
    public static Texture2D? Info       { get; private set; }
    public static Texture2D? TriAlert   { get; private set; }
    public static Texture2D? Palette    { get; private set; }
    public static Texture2D? Clapperboard { get; private set; }
    public static Texture2D? ListTree   { get; private set; }
    public static Texture2D? ZoomIn     { get; private set; }
    public static Texture2D? ZoomOut    { get; private set; }

    // ── additional icons for Content Browser ──────────────────────────────
    public static Texture2D? Grid3x3    { get; private set; }
    public static Texture2D? FileCode   { get; private set; }
    public static Texture2D? Hand       { get; private set; }
    public static Texture2D? Lock       { get; private set; }
    public static Texture2D? LockOpen   { get; private set; }
    public static Texture2D? CopyPlus   { get; private set; }
    public static Texture2D? Sliders    { get; private set; }
    public static Texture2D? CircleHelp { get; private set; }
    public static Texture2D? Camera     { get; private set; }
    public static Texture2D? Volume     { get; private set; }
    public static Texture2D? Lightbulb  { get; private set; }
    public static Texture2D? Terminal   { get; private set; }
    public static Texture2D? MousePtr   { get; private set; }

    private const string Prefix = "icons/png-white/";

    /// <summary>
    /// Loads all icon textures from the MonoGame Content pipeline.
    /// Must be called inside <c>Game1.LoadContent()</c>.
    /// </summary>
    public static void Load(ContentManager content)
    {
        static Texture2D? Try(ContentManager c, string name)
        {
            try   { return c.Load<Texture2D>(name); }
            catch { return null; }
        }

        Save         = Try(content, Prefix + "save");
        SaveAll      = Try(content, Prefix + "save-all");
        FolderOpen   = Try(content, Prefix + "folder-open");

        Move         = Try(content, Prefix + "move");
        Rotate       = Try(content, Prefix + "rotate-3d");
        Scale        = Try(content, Prefix + "scaling");
        Focus        = Try(content, Prefix + "focus");
        Maximize     = Try(content, Prefix + "maximize-2");
        Magnet       = Try(content, Prefix + "magnet");

        Play         = Try(content, Prefix + "play");
        Pause        = Try(content, Prefix + "pause");
        Redo         = Try(content, Prefix + "redo-2");
        Undo         = Try(content, Prefix + "undo-2");
        RefreshCw    = Try(content, Prefix + "refresh-cw");

        FilePlus     = Try(content, Prefix + "file-plus");
        Trash        = Try(content, Prefix + "trash-2");
        Pencil       = Try(content, Prefix + "pencil");
        Copy         = Try(content, Prefix + "copy");
        Paste        = Try(content, Prefix + "clipboard-paste");
        Scissors     = Try(content, Prefix + "scissors");

        Eye          = Try(content, Prefix + "eye");
        EyeOff       = Try(content, Prefix + "eye-off");
        Settings     = Try(content, Prefix + "settings");
        Search       = Try(content, Prefix + "search");
        Package      = Try(content, Prefix + "package");
        Layers       = Try(content, Prefix + "layers-3");
        Folder       = Try(content, Prefix + "folder");
        Image        = Try(content, Prefix + "image");
        Square       = Try(content, Prefix + "square");
        Box          = Try(content, Prefix + "box");
        Close        = Try(content, Prefix + "x");
        Info         = Try(content, Prefix + "info");
        TriAlert     = Try(content, Prefix + "triangle-alert");
        Palette      = Try(content, Prefix + "palette");
        Clapperboard = Try(content, Prefix + "clapperboard");
        ListTree     = Try(content, Prefix + "list-tree");
        ZoomIn       = Try(content, Prefix + "zoom-in");
        ZoomOut      = Try(content, Prefix + "zoom-out");

        Grid3x3      = Try(content, Prefix + "grid-3x3");
        FileCode     = Try(content, Prefix + "file-code-2");
        Hand         = Try(content, Prefix + "hand");
        Lock         = Try(content, Prefix + "lock");
        LockOpen     = Try(content, Prefix + "lock-open");
        CopyPlus     = Try(content, Prefix + "copy-plus");
        Sliders      = Try(content, Prefix + "sliders-horizontal");
        CircleHelp   = Try(content, Prefix + "circle-help");
        Camera       = Try(content, Prefix + "camera");
        Volume       = Try(content, Prefix + "volume-2");
        Lightbulb    = Try(content, Prefix + "lightbulb");
        Terminal     = Try(content, Prefix + "terminal");
        MousePtr     = Try(content, Prefix + "mouse-pointer");
    }
}
