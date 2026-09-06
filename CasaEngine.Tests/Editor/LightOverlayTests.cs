using CasaEngine.Editor.Runtime.Overlays;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class LightOverlayTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void LightOverlayIcons_AreExposedAndLoadedByEditorIcons()
    {
        string editorIcons = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "EditorIcons.cs"));

        Assert.Contains("Texture2D Lightbulb", editorIcons, StringComparison.Ordinal);
        Assert.Contains("Texture2D Cone", editorIcons, StringComparison.Ordinal);
        Assert.Contains("Texture2D Sun", editorIcons, StringComparison.Ordinal);
        Assert.Contains("Lightbulb    = Try(content, Prefix + \"lightbulb\")", editorIcons, StringComparison.Ordinal);
        Assert.Contains("Cone         = Try(content, Prefix + \"cone\")", editorIcons, StringComparison.Ordinal);
        Assert.Contains("Sun          = Try(content, Prefix + \"sun\")", editorIcons, StringComparison.Ordinal);
    }

    [Fact]
    public void LightOverlayIcons_AreReferencedByContentPipeline()
    {
        string content = File.ReadAllText(Path.Combine(RepoRoot, "CasaEngine.Editor", "Content", "Content.mgcb"));

        Assert.Contains("#begin icons/png-white/lightbulb.png", content, StringComparison.Ordinal);
        Assert.Contains("/build:icons/png-white/lightbulb.png", content, StringComparison.Ordinal);
        Assert.Contains("#begin icons/png-white/cone.png", content, StringComparison.Ordinal);
        Assert.Contains("/build:icons/png-white/cone.png", content, StringComparison.Ordinal);
        Assert.Contains("#begin icons/png-white/sun.png", content, StringComparison.Ordinal);
        Assert.Contains("/build:icons/png-white/sun.png", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LightOverlayIconMapping_UsesExpectedIconNames()
    {
        Assert.Equal(EditorLightBillboardOverlayRenderer.PointIconName, EditorLightBillboardOverlayRenderer.GetIconName(LightType.Point));
        Assert.Equal(EditorLightBillboardOverlayRenderer.SpotIconName, EditorLightBillboardOverlayRenderer.GetIconName(LightType.Spot));
        Assert.Equal(EditorLightBillboardOverlayRenderer.DirectionalIconName, EditorLightBillboardOverlayRenderer.GetIconName(LightType.Directional));
    }

    [Fact]
    public void LightOverlayCollector_ReusesListAndCollectsEveryLightComponent()
    {
        var world = new World();
        var entity = new Entity { Name = "Lights" };
        var point = new LightComponent { Type = LightType.Point, Range = 3.0f, Color = Color.Red };
        var spot = new LightComponent { Type = LightType.Spot, Range = 5.0f, Color = Color.Green };

        entity.AddComponent(point);
        entity.AddComponent(spot);
        world.Entities.Add(entity);

        var collector = new EditorLightOverlayCollector();
        var firstResult = collector.Collect(world, null, null);
        var secondResult = collector.Collect(world, null, null);

        Assert.Same(firstResult, secondResult);
        Assert.Equal(2, secondResult.Count);
        Assert.Contains(secondResult, item => ReferenceEquals(item.Light, point) && item.Type == LightType.Point && item.Range == 3.0f);
        Assert.Contains(secondResult, item => ReferenceEquals(item.Light, spot) && item.Type == LightType.Spot && item.Range == 5.0f);
    }

    [Fact]
    public void LightOverlayCollector_SelectsEntityLightsOrSpecificLightComponent()
    {
        var world = new World();
        var entity = new Entity { Name = "Lights" };
        var point = new LightComponent { Type = LightType.Point };
        var spot = new LightComponent { Type = LightType.Spot };
        var nonLight = new TestComponent();

        entity.AddComponent(point);
        entity.AddComponent(spot);
        entity.AddComponent(nonLight);
        world.Entities.Add(entity);

        var collector = new EditorLightOverlayCollector();

        var entitySelection = collector.Collect(world, entity, null);
        Assert.All(entitySelection, item => Assert.True(item.IsSelected));

        var lightSelection = collector.Collect(world, entity, point);
        Assert.True(GetItem(lightSelection, point).IsSelected);
        Assert.False(GetItem(lightSelection, spot).IsSelected);

        var nonLightSelection = collector.Collect(world, entity, nonLight);
        Assert.All(nonLightSelection, item => Assert.False(item.IsSelected));
    }

    private static EditorLightOverlayItem GetItem(IReadOnlyList<EditorLightOverlayItem> items, LightComponent light)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index].Light, light))
            {
                return items[index];
            }
        }

        throw new InvalidOperationException("The expected light overlay item was not collected.");
    }

    private sealed class TestComponent : EntityComponent
    {
        public override EntityComponent Clone() => new TestComponent();
    }
}