using CasaEngine.Core.Logging;
using CasaEngine.Editor;
using CasaEngine.Editor.Log;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class EditorAutomationOutcomeTests
{
    private static DiagnosticEntry Entry(string message, LogVerbosity verbosity = LogVerbosity.Info)
        => new(DateTime.Now, verbosity, message);

    [Fact]
    public void ComputeExitCode_IsZero_WhenEveryStepSucceeded()
    {
        var entries = new[]
        {
            Entry("[Automation] Opened asset='UI/Screens/Splash/Splash.uiscreen'"),
            Entry("[Automation] Unknown dock panel alias 'nope'.", LogVerbosity.Warning),
        };

        Assert.Equal(EditorAutomationOutcome.Success, EditorAutomationOutcome.ComputeExitCode(entries, new[] { "PASS - play session started" }, timedOut: false));
    }

    [Theory]
    [InlineData("[Automation] Unable to open asset='Audio/Beep.wav' (resolved='x')")]
    [InlineData("[Automation] Failed to update material property 'alpha_cutoff': no such property")]
    [InlineData("[Automation] Refused to update material property 'alpha_cutoff': no file")]
    public void ComputeExitCode_IsOne_WhenARequestedStepFailed(string message)
    {
        Assert.Equal(EditorAutomationOutcome.Failure, EditorAutomationOutcome.ComputeExitCode(new[] { Entry(message, LogVerbosity.Warning) }, Array.Empty<string>(), timedOut: false));
    }

    [Fact]
    public void ComputeExitCode_IsOne_WhenAPlaySmokeCheckFailed()
    {
        Assert.Equal(EditorAutomationOutcome.Failure, EditorAutomationOutcome.ComputeExitCode(Array.Empty<DiagnosticEntry>(), new[] { "PASS - a", "FAIL - play never reached running state" }, timedOut: false));
    }

    [Fact]
    public void ComputeExitCode_IsTwo_WhenTheRunTimedOut()
    {
        Assert.Equal(EditorAutomationOutcome.TimedOut, EditorAutomationOutcome.ComputeExitCode(new[] { Entry("[Automation] Unable to open asset='x'") }, Array.Empty<string>(), timedOut: true));
    }
}
