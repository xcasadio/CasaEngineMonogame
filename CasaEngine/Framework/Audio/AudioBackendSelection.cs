namespace CasaEngine.Framework.Audio;

/// <summary>Where the chosen audio backend comes from.</summary>
public enum AudioBackendSource
{
    Environment,
    Project,
    Default
}

/// <summary>Outcome of <see cref="AudioBackendSelection.Resolve"/>.</summary>
/// <param name="Kind">The chosen backend.</param>
/// <param name="Source">Which input decided it.</param>
/// <param name="Warning">Set when the environment value was non-empty but unknown (and so ignored); otherwise null.</param>
public readonly record struct AudioBackendSelectionResult(AudioBackendKind Kind, AudioBackendSource Source, string Warning);

/// <summary>
/// Chooses the audio backend at startup: the environment variable first, then the project
/// setting, then <see cref="DefaultKind"/>. Pure and testable; the caller reads the environment.
/// </summary>
public static class AudioBackendSelection
{
    /// <summary>Environment variable that overrides the project setting, for every host.</summary>
    public const string EnvironmentVariableName = "CASAENGINE_AUDIO_BACKEND";

    /// <summary>
    /// Backend used when neither the environment nor the project says anything: the engine's
    /// software mixer since the author's listening test of 2026-10-05 (ADR-0055).
    /// </summary>
    public const AudioBackendKind DefaultKind = AudioBackendKind.Software;

    /// <param name="environmentValue">Value of <see cref="EnvironmentVariableName"/>, or null.</param>
    /// <param name="projectSetting">The project's <c>AudioBackend</c>, or null when not set or no project is loaded.</param>
    public static AudioBackendSelectionResult Resolve(string environmentValue, AudioBackendKind? projectSetting)
    {
        string warning = null;

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            string trimmed = environmentValue.Trim();
            if (Enum.TryParse(trimmed, ignoreCase: true, out AudioBackendKind parsed)
                && Enum.IsDefined(parsed)
                && !int.TryParse(trimmed, out _))
            {
                return new AudioBackendSelectionResult(parsed, AudioBackendSource.Environment, null);
            }

            warning = $"{EnvironmentVariableName}='{environmentValue}' is not a known audio backend (expected Software or MonoGame), ignored.";
        }

        if (projectSetting.HasValue)
        {
            return new AudioBackendSelectionResult(projectSetting.Value, AudioBackendSource.Project, warning);
        }

        return new AudioBackendSelectionResult(DefaultKind, AudioBackendSource.Default, warning);
    }
}
