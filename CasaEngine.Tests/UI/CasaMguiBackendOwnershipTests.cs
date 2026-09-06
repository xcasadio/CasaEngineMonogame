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

        Assert.DoesNotContain("MGUI.MonoGame.Integration.csproj", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("MGUI.MonoGame.LegacyRenderer.csproj", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeAndEditorBootstrap_Use_CasaOwnedBackendTypes()
    {
        string uiRootText = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "UIRoot.cs"));
        string editorGameText = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "GameEditor.cs"));

        Assert.Contains("CasaMonoGameBackendBootstrap.Create", uiRootText, StringComparison.Ordinal);
        Assert.Contains("CasaMonoGameBackendBootstrap.Create", editorGameText, StringComparison.Ordinal);
        Assert.Contains("CasaGameRenderHost<GameEditor>", editorGameText, StringComparison.Ordinal);
    }

    [Fact]
    public void MguiSharedAndCore_DoNotReference_AposShapes_Or_NvgSharp()
    {
        string[] sourceRoots =
        {
            Path.Combine(RepoRoot, "MGUI", "MGUI.Shared"),
            Path.Combine(RepoRoot, "MGUI", "MGUI.Core"),
        };

        string[] forbiddenTokens =
        {
            "Apos.Shapes",
            "NvgSharp",
        };

        List<string> violations = CollectForbiddenTokenViolations(sourceRoots, forbiddenTokens);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void NonEditorRuntimeSourcePaths_DoNotReference_NvgSharp()
    {
        string[] sourceRoots =
        {
            Path.Combine(RepoRoot, "CasaEngine"),
            Path.Combine(RepoRoot, "CasaEngine.Demos"),
            Path.Combine(RepoRoot, "CasaEngine.EditorServices"),
        };

        List<string> violations = CollectForbiddenTokenViolations(sourceRoots, new[] { "NvgSharp" });
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CasaEngineCoreRuntimeSourcePaths_DoNotReference_AposShapes()
    {
        string[] sourceRoots =
        {
            Path.Combine(RepoRoot, "CasaEngine"),
        };

        List<string> violations = CollectForbiddenTokenViolations(sourceRoots, new[] { "Apos.Shapes" });
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
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
        string documentation = File.ReadAllText(Path.Combine(RepoRoot, "docs", "engine", "casaengine-mgui-backend.md"));
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

    [Fact]
    public void ExtensibilityArchitectureDocument_Lists_TargetLayers_And_ValidationMatrix()
    {
        string documentation = File.ReadAllText(Path.Combine(RepoRoot, "docs", "engine", "casaengine-mgui-backend-extensibility.md"));

        string[] expectedFragments =
        {
            "## Target layering",
            "`IShapeRenderer2D`",
            "`OverlayViewPipeline` still exposes a dedicated vector overlay stage that runs before MGUI composition.",
            "## Surface target model",
            "## Validation matrix",
            "`UIOverlayDemo`",
            "`WorldSpaceUIDemo`",
        };

        foreach (string fragment in expectedFragments)
        {
            Assert.Contains(fragment, documentation, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PackageIsolation_UsesAposInOptionalBackend_AndNvgInEditorOnly()
    {
        string engineProject = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "CasaEngine.csproj"));
        string aposBackendProject = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.AposShapes", "CasaEngine.AposShapes.csproj"));
        string editorProject = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "CasaEngine.Editor.csproj"));

        Assert.DoesNotContain("Apos.Shapes", engineProject, StringComparison.Ordinal);
        Assert.Contains("Apos.Shapes", aposBackendProject, StringComparison.Ordinal);
        Assert.DoesNotContain("NvgSharp", engineProject, StringComparison.Ordinal);
        Assert.Contains("NvgSharp", editorProject, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendOptions_ExposeAposFactoryHelper()
    {
        string optionsFile = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "Backend", "MonoGame", "CasaMonoGameBackendOptions.cs"));

        Assert.Contains("CasaEngine.AposShapes", optionsFile, StringComparison.Ordinal);
        Assert.Contains("CreateOptionalAposShapeRenderer", optionsFile, StringComparison.Ordinal);
    }

    [Fact]
    public void CasaDrawTransaction_PublicPrimitiveEntryPoints_RouteThroughShapeRenderer()
    {
        string drawTransaction = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "Backend", "MonoGame", "CasaDrawTransaction.cs"));

        string[] expectedFragments =
        {
            "=> ShapeRenderer.FillRectangle",
            "=> ShapeRenderer.StrokeRectangle",
            "=> ShapeRenderer.StrokeAndFillCircle",
            "=> ShapeRenderer.StrokeLineSegment",
            "=> ShapeRenderer.FillTriangle",
            "=> ShapeRenderer.FillQuadrilateralLinearClamp",
        };

        foreach (string fragment in expectedFragments)
        {
            Assert.Contains(fragment, drawTransaction, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OverlayPipeline_RendersVectorPass_BeforeUiComposition()
    {
        string pipeline = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "Runtime", "Rendering", "OverlayViewPipeline.cs"));

        int vectorIndex = pipeline.IndexOf("RenderVectorOverlay(graphicsDevice, view, in frame);", StringComparison.Ordinal);
        int uiIndex = pipeline.IndexOf("RenderUIOverlay(graphicsDevice, view, in frame);", StringComparison.Ordinal);

        Assert.True(vectorIndex >= 0, "The vector overlay stage should be present in OverlayViewPipeline.");
        Assert.True(uiIndex >= 0, "The UI overlay stage should be present in OverlayViewPipeline.");
        Assert.True(vectorIndex < uiIndex, "The vector overlay stage must execute before UI composition.");
    }

    [Fact]
    public void ClipManager_Delegates_Strategies_To_DedicatedExecutors()
    {
        string clipManager = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "Backend", "MonoGame", "Clipping", "CasaClipManager.cs"));

        Assert.Contains("CasaScissorClipExecutor", clipManager, StringComparison.Ordinal);
        Assert.Contains("CasaStencilClipExecutor", clipManager, StringComparison.Ordinal);
        Assert.Contains("CasaMaskClipExecutor", clipManager, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderTargetService_EndsCurrentContext_BeforeSwitchingTargets()
    {
        string renderTargetService = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "Backend", "MonoGame", "CasaRenderTargetService.cs"));

        int endContextIndex = renderTargetService.IndexOf("_owner.EndCurrentContext();", StringComparison.Ordinal);
        int setTargetIndex = renderTargetService.IndexOf("_owner.GraphicsDevice.SetRenderTarget(renderTarget);", StringComparison.Ordinal);

        Assert.True(endContextIndex >= 0, "The render target service should end the active draw context before switching targets.");
        Assert.True(setTargetIndex >= 0, "The render target service should set the new render target explicitly.");
        Assert.True(endContextIndex < setTargetIndex, "The active draw context must be ended before GraphicsDevice.SetRenderTarget is called.");
    }

    [Fact]
    public void MaskClipExecutor_UsesTemporaryRenderTargetPool_And_ReturnsTargets()
    {
        string maskExecutor = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine", "Framework", "UI", "Backend", "MonoGame", "Clipping", "CasaMaskClipExecutor.cs"));

        Assert.Contains("RenderTargetPool.Rent", maskExecutor, StringComparison.Ordinal);
        Assert.Contains("RenderTargetPool.Return", maskExecutor, StringComparison.Ordinal);
    }

    private static List<string> CollectForbiddenTokenViolations(IEnumerable<string> sourceRoots, IEnumerable<string> forbiddenTokens)
    {
        List<string> violations = new();

        foreach (string filePath in EnumerateSourceFiles(sourceRoots))
        {
            string content = File.ReadAllText(filePath);
            foreach (string token in forbiddenTokens)
            {
                if (content.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(RepoRoot, filePath)} -> {token}");
                }
            }
        }

        return violations;
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