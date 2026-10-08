using System;
using System.Globalization;
using System.Threading;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Demos;

/// <summary>
/// <c>CASAENGINE_DEMO_CYCLE</c>: goes from the main screen to every demo and back, twice, through the same paths as the
/// player (the Launch request with its loading frame, and the return to the menu that Esc and Select ask for), then
/// exits with code 0 when every check passed and 1 otherwise. The value is the number of frames each world runs
/// (default 60). See <c>ai-agent/tasks/demos-main-menu-tasks.md</c>, T1.1 and T2.3.
/// <para/>
/// After each world change it checks that the current world is a new instance, that the world left behind holds no
/// entity, that the camera of every view belongs to the new world, that the camera the demo (or the menu) created is the
/// camera of a view, and that no error and no default-camera warning was logged since the previous step. For the menu
/// world it also checks that there is one view and that the main screen was pushed once. A logger counts the errors and
/// exceptions logged during the run, and the warning the view bootstrapper writes when it has to create a default
/// camera (the camera was not in the world when its views were built).
/// <para/>
/// During the loading frame of every launch it asks for the same demo again, as a second click or a double click would,
/// and fails when a world it did not ask for loads (the game must drop that request).
/// </summary>
internal sealed class DemoCycleAutomation
{
    /// <summary>Returned by <see cref="Update"/> to ask for the main screen.</summary>
    public const int MenuTarget = -2;

    private const string VariableName = "CASAENGINE_DEMO_CYCLE";
    private const int DefaultFramesPerWorld = 60;
    private const int Rounds = 2;
    private const string DefaultCameraWarning = "No camera found in the world";

    private readonly int _framesPerWorld;
    private readonly int _demoCount;
    private readonly int _totalDemoVisits;
    private readonly CountingLogger _logger = new();
    private int _nextDemo;
    private int _demoVisits;
    private int _step;
    private int _framesInWorld;
    // The demo launched from the main screen, asked again during its loading frame; -1 when there is none.
    private int _relaunchDemo = -1;
    private bool _worldLoaded;
    private bool _currentIsMenu;
    private bool _finished;
    private int _failedChecks;
    private int _errorsAtLastStep;
    private int _defaultCameraWarningsAtLastStep;
    private World _outgoingWorld;

    private DemoCycleAutomation(int framesPerWorld, int demoCount, int firstDemoIndex)
    {
        _framesPerWorld = framesPerWorld;
        _demoCount = demoCount;
        _nextDemo = firstDemoIndex;
        _totalDemoVisits = demoCount * Rounds;
        Logs.AddLogger(_logger);
    }

    /// <summary>Creates the cycle when <c>CASAENGINE_DEMO_CYCLE</c> is set, or returns null.</summary>
    /// <param name="firstDemoIndex">The first demo the cycle launches (or the demo the game starts in).</param>
    public static DemoCycleAutomation TryCreate(int demoCount, int firstDemoIndex)
    {
        var value = Environment.GetEnvironmentVariable(VariableName);
        if (string.IsNullOrWhiteSpace(value) || demoCount == 0)
        {
            return null;
        }

        int framesPerWorld = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 1
            ? parsed
            : DefaultFramesPerWorld;
        Logs.WriteInfo($"[DemoCycle] {demoCount} demos, {Rounds} rounds, {framesPerWorld} frames per world, first demo {firstDemoIndex}");
        return new DemoCycleAutomation(framesPerWorld, demoCount, firstDemoIndex);
    }

    public bool IsFinished => _finished;

    /// <summary>True when the run finished and every check passed.</summary>
    public bool Passed => _failedChecks == 0 && _logger.Errors == 0 && _logger.DefaultCameraWarnings == 0;

    /// <summary>
    /// Called once per update. Once the current world has run its frames, returns what to load next: the index of a demo
    /// (from the main screen), <see cref="MenuTarget"/> (from a demo), or -1 (nothing yet, or the run is over). The update
    /// after a launch, the loading frame, returns the same demo again.
    /// </summary>
    public int Update()
    {
        if (_finished)
        {
            return -1;
        }

        if (!_worldLoaded)
        {
            int relaunch = _relaunchDemo;
            _relaunchDemo = -1;
            return relaunch;
        }

        if (++_framesInWorld < _framesPerWorld)
        {
            return -1;
        }

        if (!_currentIsMenu)
        {
            _worldLoaded = false;
            return MenuTarget;
        }

        if (_demoVisits >= _totalDemoVisits)
        {
            Finish();
            return -1;
        }

        _worldLoaded = false;
        int demo = _nextDemo;
        _nextDemo = (_nextDemo + 1) % _demoCount;
        _demoVisits++;
        _relaunchDemo = demo;
        return demo;
    }

    /// <summary>Called by the game right before it clears the world it leaves.</summary>
    public void OnWorldLeaving(World outgoingWorld)
    {
        _outgoingWorld = outgoingWorld;
    }

    /// <summary>Called when the world just loaded is ready: its views exist and its camera is initialized.</summary>
    /// <param name="title">The demo title, or the menu window title.</param>
    /// <param name="ownCamera">The camera the demo (or the menu) created for its world.</param>
    /// <param name="isMenu">True for the menu world.</param>
    /// <param name="mainScreenPushes">How many main screens were pushed for this menu world.</param>
    /// <param name="demoIndex">The demo just loaded, or -1 for the menu.</param>
    public void OnWorldLoaded(GameManager gameManager, string title, CameraComponent ownCamera, bool isMenu, int mainScreenPushes, int demoIndex)
    {
        var world = gameManager.CurrentWorld;
        int failures = 0;

        if (_worldLoaded)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': this world was not asked for (for example a launch asked during the loading frame was not dropped)");
        }

        if (_outgoingWorld != null && ReferenceEquals(world, _outgoingWorld))
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': the current world is the world it replaced");
        }

        if (_outgoingWorld != null && _outgoingWorld.Entities.Count != 0)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': the previous world still holds {_outgoingWorld.Entities.Count} entities");
        }

        int viewCount = 0;
        bool ownCameraInView = ownCamera == null;
        foreach (var view in gameManager.ViewManager.Views)
        {
            viewCount++;
            var cameraWorld = view.Camera?.Owner?.World;
            if (!ReferenceEquals(cameraWorld, world))
            {
                failures++;
                Logs.WriteInfo($"[DemoCycle] FAIL '{title}': view {viewCount} has a camera from another world (or none)");
            }

            ownCameraInView |= ReferenceEquals(view.Camera, ownCamera);
        }

        if (viewCount == 0 || (isMenu && viewCount != 1))
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': {viewCount} view(s)");
        }

        if (!ownCameraInView)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': the camera created for this world is the camera of no view");
        }

        if (isMenu && mainScreenPushes != 1)
        {
            failures++;
            Logs.WriteInfo($"[DemoCycle] FAIL '{title}': the main screen was pushed {mainScreenPushes} time(s)");
        }

        int errors = _logger.Errors;
        int defaultCameraWarnings = _logger.DefaultCameraWarnings;
        if (errors != _errorsAtLastStep || defaultCameraWarnings != _defaultCameraWarningsAtLastStep)
        {
            failures++;
            Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
                $"[DemoCycle] FAIL '{title}': {errors - _errorsAtLastStep} error(s) and {defaultCameraWarnings - _defaultCameraWarningsAtLastStep} default-camera warning(s) since the previous step"));
        }

        _errorsAtLastStep = errors;
        _defaultCameraWarningsAtLastStep = defaultCameraWarnings;

        // A run started in a demo (CASAENGINE_START_DEMO) counts that demo as its first visit.
        if (_step == 0 && !isMenu && demoIndex >= 0)
        {
            _demoVisits++;
            _nextDemo = (demoIndex + 1) % _demoCount;
        }

        _step++;
        _failedChecks += failures;
        Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
            $"[DemoCycle] step {_step} {(isMenu ? "menu" : "demo")} '{title}': {(failures == 0 ? "OK" : "FAIL")}, entities={world.Entities.Count}, views={viewCount}, demo visits={_demoVisits}/{_totalDemoVisits}"));

        _outgoingWorld = null;
        _currentIsMenu = isMenu;
        _worldLoaded = true;
        _framesInWorld = 0;
    }

    private void Finish()
    {
        _finished = true;
        Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
            $"[DemoCycle] result={(Passed ? "PASS" : "FAIL")} steps={_step} failedChecks={_failedChecks} errors={_logger.Errors} defaultCameraWarnings={_logger.DefaultCameraWarnings}"));
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
