namespace CasaEngine.Engine;

public static class EngineEnvironment
{
    public static string? ProjectPath { get; set; } = Environment.CurrentDirectory;

    public static string ResolveProjectPath(string? projectPath)
    {
        return string.IsNullOrWhiteSpace(projectPath)
            ? Environment.CurrentDirectory
            : projectPath;
    }
}