using Xunit;

namespace CasaEngine.Tests.Rendering;

public class MonoGameBasicEffectUsageTests
{
    private static readonly string[] SourceRootsToScan =
    {
        "CasaEngine",
        "CasaEngine.Demos",
        "CasaEngine.Editor",
        "CasaEngine.EditorServices",
        "GizmoTool",
    };

    [Fact]
    public void RuntimeAndToolingSources_DoNotReferenceMonoGameBasicEffect()
    {
        string repositoryRoot = FindRepositoryRoot();
        var offenders = new List<string>();

        foreach (string sourceRoot in SourceRootsToScan)
        {
            string sourceDirectory = Path.Combine(repositoryRoot, sourceRoot);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            foreach (string filePath in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedDirectory(filePath))
                {
                    continue;
                }

                int lineNumber = 0;
                foreach (string line in File.ReadLines(filePath))
                {
                    lineNumber++;
                    if (!line.Contains("BasicEffect", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string relativePath = Path.GetRelativePath(repositoryRoot, filePath).Replace('\\', '/');
                    offenders.Add($"{relativePath}:{lineNumber}: {line.Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "MonoGame BasicEffect references remain in runtime/tooling sources:\n" + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsGeneratedDirectory(string filePath)
        => filePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
           || filePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}