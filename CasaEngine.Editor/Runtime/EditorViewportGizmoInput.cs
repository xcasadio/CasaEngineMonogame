using CasaEngine.Framework.Scene.Cameras;
using GizmoTools;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Runtime;

/// <summary>When the viewport picks entities this frame, and how the pick changes the selection.</summary>
internal enum ViewportSelectionPick
{
    None,

    /// <summary>2D and front-2D views (no navigation result): the left press picks (D9).</summary>
    OnPress,

    /// <summary>Perspective view: a left click released without dragging picks (ADR-0089).</summary>
    OnClickRelease,
}

internal readonly record struct ViewportSelectionRequest(ViewportSelectionPick Pick, bool AddToSelection, bool RemoveFromSelection)
{
    public static readonly ViewportSelectionRequest None = new(ViewportSelectionPick.None, false, false);
}

/// <summary>
/// The keyboard and selection rules of <see cref="EditorViewportGizmoController"/>, free of any graphics object so
/// that they can be unit tested (ADR-0089, notice A5).
/// </summary>
internal static class EditorViewportGizmoInput
{
    /// <summary>
    /// Gizmo mode asked by the keyboard this frame, or <c>null</c>. W, E and R work like Unreal, except with the
    /// right button held (they fly the camera) or Ctrl held (editor shortcuts); 4 picks the uniform scale. When
    /// several keys go down together, the last in the order W, E, R, 4 wins.
    /// </summary>
    public static GizmoMode? ReadModeKey(in KeyboardState current, in KeyboardState previous, bool isKeyboardFocused, bool rightButtonHeld)
    {
        if (!isKeyboardFocused)
        {
            return null;
        }

        GizmoMode? mode = null;
        bool letterKeysEnabled = !rightButtonHeld
            && !current.IsKeyDown(Keys.LeftControl)
            && !current.IsKeyDown(Keys.RightControl);

        if (letterKeysEnabled && IsNewKeyPress(current, previous, Keys.W))
        {
            mode = GizmoMode.Translate;
        }

        if (letterKeysEnabled && IsNewKeyPress(current, previous, Keys.E))
        {
            mode = GizmoMode.Rotate;
        }

        if (letterKeysEnabled && IsNewKeyPress(current, previous, Keys.R))
        {
            mode = GizmoMode.NonUniformScale;
        }

        if (IsNewKeyPress(current, previous, Keys.D4))
        {
            mode = GizmoMode.UniformScale;
        }

        return mode;
    }

    /// <summary>
    /// Selection pick of this frame. Without a navigation result (2D views) the left press picks, Ctrl adds and Alt
    /// removes; with one (perspective) a released click picks, Ctrl or Shift adds and nothing removes.
    /// </summary>
    public static ViewportSelectionRequest ReadSelection(
        in KeyboardState keyboard,
        bool leftJustPressed,
        ViewportNavigationResult? navigationResult,
        bool allowSelectionPicking)
    {
        if (!allowSelectionPicking)
        {
            return ViewportSelectionRequest.None;
        }

        bool controlHeld = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);

        if (!navigationResult.HasValue)
        {
            if (!leftJustPressed)
            {
                return ViewportSelectionRequest.None;
            }

            bool altHeld = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
            return new ViewportSelectionRequest(ViewportSelectionPick.OnPress, controlHeld, altHeld);
        }

        if (!navigationResult.Value.LeftClickReleased)
        {
            return ViewportSelectionRequest.None;
        }

        bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        return new ViewportSelectionRequest(ViewportSelectionPick.OnClickRelease, controlHeld || shiftHeld, false);
    }

    private static bool IsNewKeyPress(in KeyboardState current, in KeyboardState previous, Keys key)
    {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }
}
