using System;
using System.Collections.Generic;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// MGUI "Controls" panel reproducing the lil-gui panel of the three.js skeletal
/// animation blending example. The panel only builds the widgets and raises events;
/// all playback logic lives in <see cref="SkeletalAnimationBlendingDemo"/>.
/// </summary>
internal sealed class BlendingControlsScreen : UIScreenBase
{
    private MGWindow? _window;

    private MGSlider? _idleWeightSlider;
    private MGSlider? _walkWeightSlider;
    private MGSlider? _runWeightSlider;

    private MGButton? _walkToIdleButton;
    private MGButton? _idleToWalkButton;
    private MGButton? _walkToRunButton;
    private MGButton? _runToWalkButton;

    private bool _suppressWeightEvents;

    public override UILayer Layer => UILayer.HUD;
    public override bool IsModal => false;

    // ---- Visibility ----
    public event Action<bool>? ShowModelChanged;
    public event Action<bool>? ShowSkeletonChanged;

    // ---- Foot Lock ----
    public event Action<bool>? FootLockChanged;

    // ---- Activation / Deactivation ----
    public event Action? DeactivateAllRequested;
    public event Action? ActivateAllRequested;

    // ---- Pausing / Stepping ----
    public event Action? PauseContinueRequested;
    public event Action? SingleStepRequested;
    public event Action<float>? StepSizeChanged;

    // ---- Crossfading ----
    public event Action? WalkToIdleRequested;
    public event Action? IdleToWalkRequested;
    public event Action? WalkToRunRequested;
    public event Action? RunToWalkRequested;
    public event Action<bool>? UseDefaultDurationChanged;
    public event Action<float>? CustomDurationChanged;

    // ---- Blend Weights ----
    public event Action<float>? IdleWeightChanged;
    public event Action<float>? WalkWeightChanged;
    public event Action<float>? RunWeightChanged;

    // ---- General Speed ----
    public event Action<float>? TimeScaleChanged;

    protected override void OnInitialize(UIRoot root)
    {
        var bounds = root.Desktop.ValidScreenBounds;
        int winW = 320;
        int winH = Math.Min(560, bounds.Height - 20);
        int x = bounds.Width - winW - 10;
        int y = 10;

        _window = new MGWindow(root.Desktop, x, y, winW, winH)
        {
            TitleText = "Controls",
            IsUserResizable = false,
        };
        _window.Padding = new Thickness(6);
        _window.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 205));

        var scroll = new MGScrollViewer(_window);
        var outer = new MGStackPanel(_window, Orientation.Vertical) { Spacing = 4 };

        outer.TryAddChild(BuildVisibilityFolder());
        outer.TryAddChild(BuildFootLockFolder());
        outer.TryAddChild(BuildActivationFolder());
        outer.TryAddChild(BuildPausingFolder());
        outer.TryAddChild(BuildCrossfadingFolder());
        outer.TryAddChild(BuildBlendWeightsFolder());
        outer.TryAddChild(BuildSpeedFolder());

        scroll.SetContent(outer);
        _window.SetContent(scroll);
    }

    // ---- Folders ----

    private MGExpander BuildVisibilityFolder()
    {
        var content = NewFolderContent();
        content.TryAddChild(NewCheckBox("show model", true, isChecked => ShowModelChanged?.Invoke(isChecked)));
        content.TryAddChild(NewCheckBox("show skeleton", false, isChecked => ShowSkeletonChanged?.Invoke(isChecked)));
        return NewFolder("Visibility", content);
    }

    private MGExpander BuildFootLockFolder()
    {
        var content = NewFolderContent();
        content.TryAddChild(NewCheckBox("foot lock", false, isChecked => FootLockChanged?.Invoke(isChecked)));
        return NewFolder("Foot Lock", content);
    }

    private MGExpander BuildActivationFolder()
    {
        var content = NewFolderContent();
        content.TryAddChild(NewButton("deactivate all", () => DeactivateAllRequested?.Invoke()));
        content.TryAddChild(NewButton("activate all", () => ActivateAllRequested?.Invoke()));
        return NewFolder("Activation/Deactivation", content);
    }

    private MGExpander BuildPausingFolder()
    {
        var content = NewFolderContent();
        content.TryAddChild(NewButton("pause/continue", () => PauseContinueRequested?.Invoke()));
        content.TryAddChild(NewButton("make single step", () => SingleStepRequested?.Invoke()));
        content.TryAddChild(NewSliderRow("modify step size", 0.01f, 0.1f, 0.05f, "F3",
            value => StepSizeChanged?.Invoke(value)));
        return NewFolder("Pausing/Stepping", content);
    }

    private MGExpander BuildCrossfadingFolder()
    {
        var content = NewFolderContent();
        _walkToIdleButton = NewButton("from walk to idle", () => WalkToIdleRequested?.Invoke());
        _idleToWalkButton = NewButton("from idle to walk", () => IdleToWalkRequested?.Invoke());
        _walkToRunButton = NewButton("from walk to run", () => WalkToRunRequested?.Invoke());
        _runToWalkButton = NewButton("from run to walk", () => RunToWalkRequested?.Invoke());
        content.TryAddChild(_walkToIdleButton);
        content.TryAddChild(_idleToWalkButton);
        content.TryAddChild(_walkToRunButton);
        content.TryAddChild(_runToWalkButton);
        content.TryAddChild(NewCheckBox("use default duration", true,
            isChecked => UseDefaultDurationChanged?.Invoke(isChecked)));
        content.TryAddChild(NewSliderRow("set custom duration", 0f, 10f, 3.5f, "F2",
            value => CustomDurationChanged?.Invoke(value)));
        return NewFolder("Crossfading", content);
    }

    private MGExpander BuildBlendWeightsFolder()
    {
        var content = NewFolderContent();
        _idleWeightSlider = AddWeightSlider(content, "modify idle weight", 0f, value =>
        {
            if (!_suppressWeightEvents) IdleWeightChanged?.Invoke(value);
        });
        _walkWeightSlider = AddWeightSlider(content, "modify walk weight", 1f, value =>
        {
            if (!_suppressWeightEvents) WalkWeightChanged?.Invoke(value);
        });
        _runWeightSlider = AddWeightSlider(content, "modify run weight", 0f, value =>
        {
            if (!_suppressWeightEvents) RunWeightChanged?.Invoke(value);
        });
        return NewFolder("Blend Weights", content);
    }

    private MGExpander BuildSpeedFolder()
    {
        var content = NewFolderContent();
        content.TryAddChild(NewSliderRow("modify time scale", 0f, 1.5f, 1f, "F2",
            value => TimeScaleChanged?.Invoke(value)));
        return NewFolder("General Speed", content);
    }

    // ---- External updates ----

    /// <summary>Updates the weight slider displays without re-raising change events
    /// (mirrors three.js updateWeightSliders during crossfades).</summary>
    public void SetWeightDisplays(float idle, float walk, float run)
    {
        _suppressWeightEvents = true;
        if (_idleWeightSlider != null) _idleWeightSlider.SetValue(idle);
        if (_walkWeightSlider != null) _walkWeightSlider.SetValue(walk);
        if (_runWeightSlider != null) _runWeightSlider.SetValue(run);
        _suppressWeightEvents = false;
    }

    /// <summary>Enables/disables the four crossfade buttons (mirrors
    /// three.js updateCrossFadeControls).</summary>
    public void SetCrossFadeButtonsEnabled(bool walkToIdle, bool idleToWalk, bool walkToRun, bool runToWalk)
    {
        if (_walkToIdleButton != null) _walkToIdleButton.IsEnabled = walkToIdle;
        if (_idleToWalkButton != null) _idleToWalkButton.IsEnabled = idleToWalk;
        if (_walkToRunButton != null) _walkToRunButton.IsEnabled = walkToRun;
        if (_runToWalkButton != null) _runToWalkButton.IsEnabled = runToWalk;
    }

    // ---- Widget helpers ----

    private MGExpander NewFolder(string header, MGStackPanel content)
    {
        var expander = new MGExpander(_window!)
        {
            IsExpanded = true,
            Header = new MGTextBlock(_window!, $"[b][color=white]{header}[/color][/b]")
            {
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        expander.Margin = new Thickness(0, 0, 0, 2);
        expander.SetContent(content);
        return expander;
    }

    private MGStackPanel NewFolderContent()
    {
        return new MGStackPanel(_window!, Orientation.Vertical)
        {
            Spacing = 3,
            Margin = new Thickness(8, 2, 2, 4),
        };
    }

    private MGButton NewButton(string label, Action onClick)
    {
        var button = new MGButton(_window!, _ => onClick());
        button.SetContent(new MGTextBlock(_window!, $"[color=yellow]{label}[/color]"));
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        return button;
    }

    private MGCheckBox NewCheckBox(string label, bool isChecked, Action<bool> onChanged)
    {
        var checkBox = new MGCheckBox(_window!, isChecked);
        checkBox.SetContent(new MGTextBlock(_window!, $"[color=lightgray]{label}[/color]"));
        checkBox.OnCheckStateChanged += (_, e) => onChanged(e.NewValue ?? false);
        return checkBox;
    }

    private MGStackPanel NewSliderRow(string label, float min, float max, float value, string format, Action<float> onChanged)
    {
        var row = new MGStackPanel(_window!, Orientation.Vertical) { Spacing = 1 };
        row.TryAddChild(new MGTextBlock(_window!, $"[color=lightgray]{label}[/color]"));
        var slider = new MGSlider(_window!, min, max, value)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 200,
            ShowValueLabel = true,
            ValueLabelFormat = format,
        };
        slider.ValueChanged += (_, e) => onChanged(e.NewValue);
        row.TryAddChild(slider);
        return row;
    }

    private MGSlider AddWeightSlider(MGStackPanel content, string label, float value, Action<float> onChanged)
    {
        var row = new MGStackPanel(_window!, Orientation.Vertical) { Spacing = 1 };
        row.TryAddChild(new MGTextBlock(_window!, $"[color=lightgray]{label}[/color]"));
        var slider = new MGSlider(_window!, 0f, 1f, value)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 200,
            ShowValueLabel = true,
            ValueLabelFormat = "F2",
        };
        slider.ValueChanged += (_, e) => onChanged(e.NewValue);
        row.TryAddChild(slider);
        content.TryAddChild(row);
        return slider;
    }

    public override IEnumerable<MGWindow> GetWindows()
    {
        if (_window != null) yield return _window;
    }
}
