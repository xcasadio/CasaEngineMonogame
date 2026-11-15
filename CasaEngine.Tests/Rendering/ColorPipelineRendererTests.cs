using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The renderers of the scene (particles, 3D sprites, lines) bind the colour pipeline constant from the frame, and the
/// screen sprites and text are flushed after the tonemapping (ADR-0097, ADR-0098).
/// </summary>
public class ColorPipelineRendererTests
{
    [Fact]
    public void OnlyTheScreenRenderer_IsFlushedAfterTheTonemapping()
    {
        Assert.True(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(Renderer2DComponent)));
        Assert.True(typeof(IViewFlushableRenderer).IsAssignableFrom(typeof(IPostTonemapRenderer)));

        // Particles, 3D sprites, lines and meshes live in the scene: they decode in their shaders instead.
        Assert.False(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(ParticleRendererComponent)));
        Assert.False(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(SpriteRendererComponent)));
        Assert.False(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(Line3dRendererComponent)));
        Assert.False(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(StaticMeshRendererComponent)));
        Assert.False(typeof(IPostTonemapRenderer).IsAssignableFrom(typeof(SkinnedMeshRendererComponent)));
    }

    [Fact]
    public void EachSceneRenderer_BindsTheConstantFromTheEnvironmentOfTheFrame()
    {
        string particles = LoadSource("Framework", "Application", "Components", "ParticleRendererComponent.cs");
        Assert.Contains("ShaderParameterNames.ColorPipelineIsLinear]?.SetValue(frame.Environment.LinearColorActive ? 1.0f : 0.0f)", particles);

        string lines = LoadSource("Framework", "Application", "Components", "Line3dRendererComponent.cs");
        Assert.Contains("lineCount, frame.Environment.LinearColorActive);", lines);
        Assert.Contains("ShaderParameterNames.ColorPipelineIsLinear]?.SetValue(linearColor ? 1.0f : 0.0f)", lines);

        // Sprites: the shared sprite effect and the PSX quad effect, on every flush and on the static batch (which has a
        // frame); the full-screen display has none and keeps its own colours.
        string sprites = LoadSource("Framework", "Application", "Components", "SpriteRendererComponent.cs");
        Assert.Contains("_linearColor = frame.Environment.LinearColorActive;", sprites);
        Assert.Contains("PoseColorPipeline(_effect, _linearColor);", sprites);
        Assert.Contains("PoseColorPipeline(quadEffect, _linearColor);", sprites);
        Assert.Contains("PoseColorPipeline(_effect, frame.Environment.LinearColorActive);", sprites);
        Assert.Contains("PoseColorPipeline(_effect, false);", sprites);
    }

    [Fact]
    public void TheDefaultPipeline_SkipsThePostTonemapRenderersOnlyInTheLinearScenePhase()
    {
        string source = LoadSource("Framework", "Rendering", "DefaultViewPipeline.cs");

        // RenderView (the whole view) always flushes everything; RenderScene skips them only when the view really draws
        // through the HDR target, so the legacy order and states are untouched.
        Assert.Contains("RenderSceneCore(view, in frame, renderers, skipPostTonemapRenderers: false);", source);
        Assert.Contains("skipPostTonemapRenderers: frame.Environment.LinearColorActive);", source);
        Assert.Contains("if (skipPostTonemapRenderers && renderer is IPostTonemapRenderer)", source);
    }

    [Fact]
    public void TheRenderPipeline_FlushesThePostTonemapRenderersBetweenTheTonemapAndTheInterface()
    {
        string source = LoadSource("Framework", "Rendering", "RenderPipeline.cs");

        int tonemap = source.IndexOf("DrawTonemap(hdrTarget, viewRectangle.Width", StringComparison.Ordinal);
        int flush = source.IndexOf("FlushPostTonemapRenderers(view, in frame);", StringComparison.Ordinal);
        int compose = source.IndexOf(".Compose(_graphicsDevice, view, in frame);", flush, StringComparison.Ordinal);
        Assert.True(tonemap >= 0 && flush > tonemap && compose > flush);

        // The depth test is forced off for the flush: the depth of the scene lives in the HDR target.
        int body = source.IndexOf("private void FlushPostTonemapRenderers(", StringComparison.Ordinal);
        Assert.True(body >= 0);
        Assert.Contains("_graphicsDevice.DepthStencilState = DepthStencilState.None;", source[body..(body + 700)]);
    }

    [Fact]
    public void TheScreenRenderer_DrawsWithoutDepthTestAfterTheTonemappingAndAsBeforeOtherwise()
    {
        string source = LoadSource("Framework", "Application", "Components", "Renderer2DComponent.cs");

        Assert.Contains("FlushQueues(depthTest: true);", source);
        Assert.Contains("FlushQueues(depthTest: false);", source);
        Assert.Contains("Game.GraphicsDevice.DepthStencilState = depthTest ? DepthStencilState.Default : DepthStencilState.None;", source);
        Assert.Contains("depthTest ? GameSettings.GraphicsSettings.DepthStencilState : DepthStencilState.None,", source);
    }

    [Fact]
    public void TheSkyBackground_DecodesItsGradientAndSunOnTheCpuInTheLinearPipeline()
    {
        string source = LoadSource("Framework", "Rendering", "SkyBackgroundViewPipeline.cs");

        // The gradient is drawn from a floating-point copy of its colours decoded to linear light (8 bits would crush the dark
        // end); the sun colour is decoded before its 0.65 factor; the legacy path draws the unchanged texture and colour.
        Assert.Contains("frame.Environment.LinearColorActive", source);
        Assert.Contains("SurfaceFormat.HalfVector4", source);
        Assert.Contains("new HalfVector4(ColorSpace.Decode(EvaluateGradientColor(sky, y, height)))", source);
        Assert.Contains("linearColor ? EnsureLinearGradientTexture() : _gradientTexture", source);
        Assert.Contains("linearColor ? ColorSpace.Decode(Sky.SunColor.ToVector3()) : Sky.SunColor.ToVector3()", source);
        Assert.Contains("new Color(new Vector4(sunColor * 0.65f, 1.0f))", source);
    }

    private static string LoadSource(params string[] relativeSegments)
    {
        string path = Path.Combine(new[] { FindRepositoryRoot(), "CasaEngine" }.Concat(relativeSegments).ToArray());
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

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
