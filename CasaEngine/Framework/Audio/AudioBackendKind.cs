namespace CasaEngine.Framework.Audio;

/// <summary>
/// The audio backend implementations the engine can start with (see <see cref="AudioBackendSelection"/>).
/// </summary>
public enum AudioBackendKind
{
    /// <summary>The backend built on the MonoGame sound API.</summary>
    MonoGame,

    /// <summary>The engine's own software mixer.</summary>
    Software
}
