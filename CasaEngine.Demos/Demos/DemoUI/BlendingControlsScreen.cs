using System;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// MGUI "Controls" panel reproducing the lil-gui panel of the three.js skeletal
/// animation blending example. The panel only builds the widgets and raises events;
/// all playback logic lives in <see cref="SkeletalAnimationBlendingDemo"/>.
/// <para/>
/// Its tree lives in `Content/Screens/blending-controls.xaml`: seven folders of fixed contents, none of it
/// data-driven. What stays here is where the window goes, what every control does, and the two methods the
/// demo calls back into.
/// </summary>
internal sealed class BlendingControlsScreen : XamlUIScreenBase
{
    private const int WindowWidth = 320;
    private const int MaxWindowHeight = 560;

    private MGSlider _idleWeightSlider;
    private MGSlider _walkWeightSlider;
    private MGSlider _runWeightSlider;

    private MGButton _walkToIdleButton;
    private MGButton _idleToWalkButton;
    private MGButton _walkToRunButton;
    private MGButton _runToWalkButton;

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

    public BlendingControlsScreen()
        : base(DemoScreenXaml.Source("blending-controls.xaml"))
    {
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        // Top-right corner, never taller than the viewport: both depend on a resolution the document
        // cannot know.
        var bounds = window.Desktop.ValidScreenBounds;
        window.Left = bounds.Width - WindowWidth - 10;
        window.Top = 10;
        window.WindowHeight = Math.Min(MaxWindowHeight, bounds.Height - 20);

        BindCheckBox("chkShowModel", isChecked => ShowModelChanged?.Invoke(isChecked));
        BindCheckBox("chkShowSkeleton", isChecked => ShowSkeletonChanged?.Invoke(isChecked));
        BindCheckBox("chkFootLock", isChecked => FootLockChanged?.Invoke(isChecked));
        BindCheckBox("chkUseDefaultDuration", isChecked => UseDefaultDurationChanged?.Invoke(isChecked));

        BindButton("btnDeactivateAll", () => DeactivateAllRequested?.Invoke());
        BindButton("btnActivateAll", () => ActivateAllRequested?.Invoke());
        BindButton("btnPauseContinue", () => PauseContinueRequested?.Invoke());
        BindButton("btnSingleStep", () => SingleStepRequested?.Invoke());

        _walkToIdleButton = BindButton("btnWalkToIdle", () => WalkToIdleRequested?.Invoke());
        _idleToWalkButton = BindButton("btnIdleToWalk", () => IdleToWalkRequested?.Invoke());
        _walkToRunButton = BindButton("btnWalkToRun", () => WalkToRunRequested?.Invoke());
        _runToWalkButton = BindButton("btnRunToWalk", () => RunToWalkRequested?.Invoke());

        BindSlider("sldStepSize", "F3", value => StepSizeChanged?.Invoke(value));
        BindSlider("sldCustomDuration", "F2", value => CustomDurationChanged?.Invoke(value));
        BindSlider("sldTimeScale", "F2", value => TimeScaleChanged?.Invoke(value));

        _idleWeightSlider = BindSlider("sldIdleWeight", "F2", value =>
        {
            if (!_suppressWeightEvents) IdleWeightChanged?.Invoke(value);
        });
        _walkWeightSlider = BindSlider("sldWalkWeight", "F2", value =>
        {
            if (!_suppressWeightEvents) WalkWeightChanged?.Invoke(value);
        });
        _runWeightSlider = BindSlider("sldRunWeight", "F2", value =>
        {
            if (!_suppressWeightEvents) RunWeightChanged?.Invoke(value);
        });
    }

    // ---- External updates ----

    /// <summary>Updates the weight slider displays without re-raising change events
    /// (mirrors three.js updateWeightSliders during crossfades).</summary>
    public void SetWeightDisplays(float idle, float walk, float run)
    {
        _suppressWeightEvents = true;
        _idleWeightSlider.SetValue(idle);
        _walkWeightSlider.SetValue(walk);
        _runWeightSlider.SetValue(run);
        _suppressWeightEvents = false;
    }

    /// <summary>Enables/disables the four crossfade buttons (mirrors
    /// three.js updateCrossFadeControls).</summary>
    public void SetCrossFadeButtonsEnabled(bool walkToIdle, bool idleToWalk, bool walkToRun, bool runToWalk)
    {
        _walkToIdleButton.IsEnabled = walkToIdle;
        _idleToWalkButton.IsEnabled = idleToWalk;
        _walkToRunButton.IsEnabled = walkToRun;
        _runToWalkButton.IsEnabled = runToWalk;
    }

    // ---- Wiring ----

    private MGButton BindButton(string name, Action onClick)
    {
        var button = FindControl<MGButton>(name);
        button.AddCommandHandler((_, _) => onClick());
        return button;
    }

    private void BindCheckBox(string name, Action<bool> onChanged)
    {
        var checkBox = FindControl<MGCheckBox>(name);
        checkBox.OnCheckStateChanged += (_, e) => onChanged(e.NewValue ?? false);
    }

    /// <summary>
    /// Subscribes a slider and turns its value label on.
    /// <para/>
    /// The label belongs in the markup, next to the range and the starting value it goes with, but
    /// <see cref="MGSlider.ShowValueLabel"/> and <see cref="MGSlider.ValueLabelFormat"/> have no XAML
    /// counterpart -- see `ai-agent/audits/mgui-gaps-from-xaml-screens.md`. Until they do, the format string
    /// lives here, away from the slider it formats.
    /// </summary>
    private MGSlider BindSlider(string name, string valueLabelFormat, Action<float> onChanged)
    {
        var slider = FindControl<MGSlider>(name);
        slider.ShowValueLabel = true;
        slider.ValueLabelFormat = valueLabelFormat;
        slider.ValueChanged += (_, e) => onChanged(e.NewValue);
        return slider;
    }
}
