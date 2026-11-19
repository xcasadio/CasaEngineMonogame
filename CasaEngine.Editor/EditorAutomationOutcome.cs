using System;
using System.Collections.Generic;
using CasaEngine.Editor.Log;

namespace CasaEngine.Editor;

/// <summary>
/// The process exit code of an editor automation run, so that a script can check its outcome without parsing the
/// diagnostics: 0 when every requested step succeeded, 1 when one failed, 2 when the run timed out.
/// </summary>
internal static class EditorAutomationOutcome
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int TimedOut = 2;

    // Diagnostics the automation writes when a requested step does not happen.
    private static readonly string[] FailurePrefixes =
    [
        "[Automation] Unable to open asset",
        "[Automation] Failed to ",
        "[Automation] Refused to ",
        "[Automation] Timed out",
    ];

    public static int ComputeExitCode(IReadOnlyList<DiagnosticEntry> entries, IReadOnlyList<string> playSmokeResults, bool timedOut)
    {
        if (timedOut)
        {
            return TimedOut;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var message = entries[i].Message;
            for (int j = 0; j < FailurePrefixes.Length; j++)
            {
                if (message != null && message.StartsWith(FailurePrefixes[j], StringComparison.Ordinal))
                {
                    return Failure;
                }
            }
        }

        for (int i = 0; i < playSmokeResults.Count; i++)
        {
            if (playSmokeResults[i] != null && playSmokeResults[i].StartsWith("FAIL", StringComparison.Ordinal))
            {
                return Failure;
            }
        }

        return Success;
    }
}
