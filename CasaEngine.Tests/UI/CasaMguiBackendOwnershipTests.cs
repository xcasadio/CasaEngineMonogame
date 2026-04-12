using System.Text.RegularExpressions;
using Xunit;

namespace CasaEngine.Tests.UI;

public class CasaMguiBackendOwnershipTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void CasaEngineProject_DoesNotReference_MguiMonoGameProject()
    {
        string projectText = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "CasaEngine.csproj"));

        Assert.DoesNotContain("MGUI.MonoGame.csproj", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeAndEditorBootstrap_Use_CasaOwnedBackendTypes()
    {
        string uiRootText = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "UIRoot.cs"));
        string editorGameText = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "Game1.cs"));

        Assert.Contains("CasaMonoGameBackendBootstrap.Create", uiRootText, StringComparison.Ordinal);
        Assert.Contains("CasaMonoGameBackendBootstrap.Create", editorGameText, StringComparison.Ordinal);
        Assert.Contains("CasaGameRenderHost<Game1>", editorGameText, StringComparison.Ordinal);
    }

    [Fact]
    public void NominalSourcePaths_DoNotReference_UpstreamConcreteBackendTypes()
    {
        string[] sourceRoots =
        {
            Path.Combine(RepoRoot, "CasaEngine"),
            Path.Combine(RepoRoot, "CasaEngine.Editor"),
            Path.Combine(RepoRoot, "CasaEngine.EditorServices"),
            Path.Combine(RepoRoot, "CasaEngine.Demos"),
        };

        (Regex Pattern, string Label)[] forbiddenPatterns =
        {
            (new Regex(@"\bMainRenderer\b", RegexOptions.CultureInvariant), "MainRenderer"),
            (new Regex(@"\bDrawTransaction\b", RegexOptions.CultureInvariant), "DrawTransaction"),
            (new Regex(@"\bMonoGameBackendBootstrap\b", RegexOptions.CultureInvariant), "MonoGameBackendBootstrap"),
            (new Regex(@"\bGameRenderHost<", RegexOptions.CultureInvariant), "GameRenderHost<>"),
            (new Regex(@"Desktop\.Renderer\b", RegexOptions.CultureInvariant), "Desktop.Renderer"),
        };

        List<string> violations = new();

        foreach (string filePath in EnumerateSourceFiles(sourceRoots))
        {
            if (filePath.Contains(Path.Combine("Framework", "UI", "Backend", "MonoGame"), StringComparison.Ordinal))
            {
                continue;
            }

            string content = File.ReadAllText(filePath);
            foreach ((Regex pattern, string label) in forbiddenPatterns)
            {
                if (pattern.IsMatch(content))
                {
                    violations.Add($"{Path.GetRelativePath(RepoRoot, filePath)} -> {label}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void BackendParityDocument_Lists_MainRendererResponsibilities()
    {
        string documentation = File.ReadAllText(Path.Combine(RepoRoot, "docs", "casaengine-mgui-backend.md"));
        string[] expectedRows =
        {
            "| Host / raw input / surface |",
            "| GraphicsDevice / SpriteBatch / PrimitiveBatch |",
            "| ContentManager / FontManager / AssetProvider / TextEngine |",
            "| RegisterView / UnregisterView / Views / UpdateViews / DrawViews |",
            "| ScrollMarker / solid color cache / circle cache |",
            "| Draw transaction |",
            "| Rectangle / stencil / mask clipping |",
            "| Runtime bootstrap |",
            "| Editor bootstrap |",
            "| World-space / offscreen surface |",
        };

        foreach (string row in expectedRows)
        {
            Assert.Contains(row, documentation, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(IEnumerable<string> roots)
    {
        foreach (string root in roots)
        {
            foreach (string filePath in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsIgnoredPath(filePath))
                {
                    continue;
                }

                yield return filePath;
            }
        }
    }

    private static bool IsIgnoredPath(string path)
        => path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}