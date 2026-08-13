namespace CasaEngine.EditorServices.Scripting;

public sealed class ScriptBuildResult
{
    public required bool Success { get; init; }

    /// <summary>Full path of the built gameplay dll; null when the build failed.</summary>
    public string? OutputAssemblyPath { get; init; }

    /// <summary>Directory the build wrote into (one unique directory per build).</summary>
    public required string BuildDirectory { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
