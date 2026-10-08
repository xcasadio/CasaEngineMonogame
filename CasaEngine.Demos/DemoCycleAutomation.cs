using System;
using System.Globalization;
using System.Threading;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Demos;

/// <summary>
/// <c>CASAENGINE_DEMO_CYCLE</c>: loads every demo in turn, twice, through the same path as an in-session demo change (each
/// demo in a fresh world, plan <c>ai-agent/tasks/demos-main-menu-tasks.md</c>, T1.1), checks every world change, then exits
/// with code 0 when every check passed and 1 otherwise. The value is the number of frames each demo runs (default 60).
/// <para/>
/// After each world change it checks that the current world is a new instance, that the world left behind holds no
/// entity, that the camera of every view belongs to the new world, that the camera the demo created is the camera of a
/// view, and that no error and no default-camera warning was logged since the previous step. A logger counts the errors
/// and exceptions logged during the run, and the warning the view bootstrapper writes when it has to create a default
/// camera (the demo camera was not in the world when its views were built).
/// </summary>
internal sealed class DemoCycleAutomation
{
    private const string VariableName = "CASAENGINE_DEMO_CYCLE";
    private const int DefaultFramesPerDemo = 60;
    private const int Rounds = 2;
    private const string DefaultCameraWarning = "No camera found in the world";

    private readonly int _framesPerDemo;
    private readonly int _demoCount;
    private readonly int _firstDemoIndex;
    private readonly int _totalSteps;
    private readonly CountingLogger _logger = new();
    private int _step;
    private int _framesInDemo;
    private bool _demoLoaded;
    private bool _finished;
    private int _failedChecks;
    private int _errorsAtLastStep;
    private int _defaultCameraWarningsAtLastStep;
    private World _outgoingWorld;

    private DemoCycleAutomation(int framesPerDemo, int demoCount, int firstDemoIndex)
    {
        _framesPerDemo = framesPerDemo;
        _demoCount = demoCount;
        _firstDemoIndex = firstDemoIndex;
        _totalSteps = demoCount * Rounds;
        Logs.AddLogger(_logger);
    }

    /// <summary>Creates the cycle when <c>CASAENGINE_DEMO_CYCLE</c> is set, or returns null.</summary>
    public static DemoCycleAutomation TryCreate(int demoCount, int firstDemoIndex)
    {
        var value = Environment.GetEnvironmentVariable(VariableName);
        if (string.IsNullOrWhiteSpace(value) || demoCount == 0)
        {
            return null;
        }

        int framesPerDemo = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 1
            ? parsed
            : DefaultFramesPerDemo;
        Logs.WriteInfo($"[DemoCycle] {demoCount} demos, {Rounds} rounds, {framesPerDemo} frames each, starting at demo {firstDemoIndex}");
        return new DemoCycleAutomation(framesPerDemo, demoCount, firstDemoIndex);
    }

    public bool IsFinished => _finished;

    /// <summary>True when the run finished and every check passed.</summary>
    public bool Passed => _failedChecks == 0 && _logger.Errors == 0 && _logger.DefaultCameraWarnings == 0;

    /// <summary>
    /// Called once per update, before the demo change requested by the game is applied. Returns the index of the next demo
    /// to load once the current one has run its frames, or -1.
    /// </summary>
    public int Update()
    {
        if (_finished || !_demoLoaded || ++_framesInDemo < _framesPerDemo)
        {
            return -1;
        }

        _step++;
        if (_step >= _totalSteps)
        {
            Finish();
            return -1;
        }

        _demoLoaded = false;
        _framesInDemo = 0;
        return (_firstDemoIndex + _step) % _demoCount;
    }

    /// <summary>Called by the game right before it clears the world of the demo it leaves.</summary>
    public void OnDemoLeaving(World outgoingWorld)
    {
        _outgoingWorld = outgoingWorld;
    }

    /// <summary>Called when the world of the demo just loaded is ready: its views exist and its camera is initialized.</summary>
    public void OnDemoWorldLoaded(GameManager gameManager, string demoTitle, CameraComponent demoCamera)
    {
        var world = gameManager.CurrentWorld;
        int failures = 0;

        if (_outgoingWorld != null && ReferenceEquals(world, _outgoingWorld))
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{demoTitle}': the current world is the world of the previous demo");
        }

        if (_outgoingWorld != null && _outgoingWorld.Entities.Count != 0)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{demoTitle}': the previous world still holds {_outgoingWorld.Entities.Count} entities");
        }

        int viewCount = 0;
        bool demoCameraInView = demoCamera == null;
        foreach (var view in gameManager.ViewManager.Views)
        {
            viewCount++;
            var cameraWorld = view.Camera?.Owner?.World;
            if (!ReferenceEquals(cameraWorld, world))
            {
                failures++;
                Logs.WriteInfo($"[DemoCycle] FAIL '{demoTitle}': view {viewCount} has a camera from another world (or none)");
            }

            demoCameraInView |= ReferenceEquals(view.Camera, demoCamera);
        }

        if (viewCount == 0)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{demoTitle}': no view");
        }

        if (!demoCameraInView)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{demoTitle}': the camera the demo created is the camera of no view");
        }

        int errors = _logger.Errors;
        int defaultCameraWarnings = _logger.DefaultCameraWarnings;
        if (errors != _errorsAtLastStep || defaultCameraWarnings != _defaultCameraWarningsAtLastStep)
        {
            failures++;
            Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
                $"[DemoCycle] FAIL '{demoTitle}': {errors - _errorsAtLastStep} error(s) and {defaultCameraWarnings - _defaultCameraWarningsAtLastStep} default-camera warning(s) since the previous step"));
        }

        _errorsAtLastStep = errors;
        _defaultCameraWarningsAtLastStep = defaultCameraWarnings;

        _failedChecks += failures;
        Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
            $"[DemoCycle] step {_step + 1}/{_totalSteps} '{demoTitle}': {(failures == 0 ? "OK" : "FAIL")}, entities={world.Entities.Count}, views={viewCount}"));

        _outgoingWorld = null;
        _demoLoaded = true;
        _framesInDemo = 0;
    }

    private void Finish()
    {
        _finished = true;
        Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
            $"[DemoCycle] result={(Passed ? "PASS" : "FAIL")} failedChecks={_failedChecks} errors={_logger.Errors} defaultCameraWarnings={_logger.DefaultCameraWarnings}"));
    }

    /// <summary>Counts the errors (exceptions included: <see cref="Logs.WriteException"/> writes an error) and the default
    /// camera warnings logged during the run.</summary>
    private sealed class CountingLogger : ILogger
    {
        private int _errors;
        private int _defaultCameraWarnings;

        public int Errors => Volatile.Read(ref _errors);
        public int DefaultCameraWarnings => Volatile.Read(ref _defaultCameraWarnings);

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }

        public void WriteWarning(string msg)
        {
            if (msg != null && msg.Contains(DefaultCameraWarning, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _defaultCameraWarnings);
            }
        }

        public void WriteError(string msg)
        {
            Interlocked.Increment(ref _errors);
        }
    }
}
