namespace CasaEngine.Engine.Environment;

public static class EngineEnvironment
{
    public static string? ProjectPath { get; set; } = System.Environment.CurrentDirectory;

    public static string ResolveProjectPath(string? projectPath)
    {
        return string.IsNullOrWhiteSpace(projectPath)
            ? System.Environment.CurrentDirectory
            : projectPath;
    }
}