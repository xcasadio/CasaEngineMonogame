using CasaEngine.Editor.Runtime;
using CasaEngine.Framework.Scene.Cameras;
using GizmoTools;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Notice A5 of ADR-0089: the gizmo's mode keys and selection picks, without a graphics device.</summary>
public sealed class EditorViewportGizmoInputTests
{
    private static readonly KeyboardState NoKeys = new();

    [Theory]
    [InlineData(Keys.W, GizmoMode.Translate)]
    [InlineData(Keys.E, GizmoMode.Rotate)]
    [InlineData(Keys.R, GizmoMode.NonUniformScale)]
    [InlineData(Keys.D4, GizmoMode.UniformScale)]
    public void ModeKey_PicksTheUnrealMode(Keys key, GizmoMode expected)
    {
        Assert.Equal(expected, EditorViewportGizmoInput.ReadModeKey(new KeyboardState(key), NoKeys, isKeyboardFocused: true, rightButtonHeld: false));
    }

    [Fact]
    public void ModeKey_HeldKey_IsNotANewPress()
    {
        var held = new KeyboardState(Keys.E);

        Assert.Null(EditorViewportGizmoInput.ReadModeKey(held, held, isKeyboardFocused: true, rightButtonHeld: false));
    }

    [Fact]
    public void ModeKey_WithoutKeyboardFocus_DoesNothing()
    {
        Assert.Null(EditorViewportGizmoInput.ReadModeKey(new KeyboardState(Keys.W), NoKeys, isKeyboardFocused: false, rightButtonHeld: false));
        Assert.Null(EditorViewportGizmoInput.ReadModeKey(new KeyboardState(Keys.D4), NoKeys, isKeyboardFocused: false, rightButtonHeld: false));
    }

    /// <summary>With the right button held W, E and R fly the camera; with Ctrl held they are editor shortcuts. 4 still works.</summary>
    [Theory]
    [InlineData(true, Keys.W)]
    [InlineData(true, Keys.E)]
    [InlineData(true, Keys.R)]
    [InlineData(false, Keys.LeftControl, Keys.W)]
    [InlineData(false, Keys.RightControl, Keys.R)]
    public void ModeKey_LetterKeys_AreIgnoredWhileFlyingOrWithControl(bool rightButtonHeld, params Keys[] keys)
    {
        Assert.Null(EditorViewportGizmoInput.ReadModeKey(new KeyboardState(keys), NoKeys, isKeyboardFocused: true, rightButtonHeld));
        Assert.Equal(GizmoMode.UniformScale, EditorViewportGizmoInput.ReadModeKey(new KeyboardState(Keys.D4), NoKeys, isKeyboardFocused: true, rightButtonHeld));
    }

    [Fact]
    public void ModeKey_SeveralKeysTogether_TheLastOfWER4Wins()
    {
        Assert.Equal(GizmoMode.NonUniformScale, EditorViewportGizmoInput.ReadModeKey(new KeyboardState(Keys.W, Keys.R), NoKeys, true, false));
        Assert.Equal(GizmoMode.UniformScale, EditorViewportGizmoInput.ReadModeKey(new KeyboardState(Keys.E, Keys.D4), NoKeys, true, false));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false, Keys.LeftControl)]
    [InlineData(true, false, Keys.LeftShift)]
    [InlineData(false, false, Keys.LeftAlt)]
    public void Selection_Perspective_PicksOnAReleasedClick(bool add, bool remove, params Keys[] keys)
    {
        var keyboard = new KeyboardState(keys);
        var click = new ViewportNavigationResult { LeftClickReleased = true };

        Assert.Equal(
            new ViewportSelectionRequest(ViewportSelectionPick.OnClickRelease, add, remove),
            EditorViewportGizmoInput.ReadSelection(keyboard, leftJustPressed: false, click, allowSelectionPicking: true));
        Assert.Equal(
            ViewportSelectionRequest.None,
            EditorViewportGizmoInput.ReadSelection(keyboard, leftJustPressed: true, new ViewportNavigationResult(), allowSelectionPicking: true));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false, Keys.RightControl)]
    [InlineData(false, true, Keys.LeftAlt)]
    [InlineData(false, false, Keys.LeftShift)]
    public void Selection_2dViews_PickOnThePress(bool add, bool remove, params Keys[] keys)
    {
        var keyboard = new KeyboardState(keys);

        Assert.Equal(
            new ViewportSelectionRequest(ViewportSelectionPick.OnPress, add, remove),
            EditorViewportGizmoInput.ReadSelection(keyboard, leftJustPressed: true, navigationResult: null, allowSelectionPicking: true));
        Assert.Equal(
            ViewportSelectionRequest.None,
            EditorViewportGizmoInput.ReadSelection(keyboard, leftJustPressed: false, navigationResult: null, allowSelectionPicking: true));
    }

    [Fact]
    public void Selection_PickingDisabled_PicksNothing()
    {
        var click = new ViewportNavigationResult { LeftClickReleased = true };

        Assert.Equal(ViewportSelectionRequest.None, EditorViewportGizmoInput.ReadSelection(NoKeys, true, null, allowSelectionPicking: false));
        Assert.Equal(ViewportSelectionRequest.None, EditorViewportGizmoInput.ReadSelection(NoKeys, false, click, allowSelectionPicking: false));
    }
}
