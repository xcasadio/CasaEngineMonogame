using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Rendering.Depth;
using Xunit;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// T3.1 (D4, narrowed): a <c>depth.*</c> custom property the loader does not understand warns once,
/// at load, naming the layer and the key, and the current default applies - never a load error. Uses
/// the engine's own log seam, like <c>ScrollingLayerComponentLoggingTests</c>: <see cref="Logs.AddLogger"/>
/// to attach a capturing <see cref="ILogger"/>, <see cref="Logs.Close"/> to detach it in <c>finally</c>.
/// <see cref="ProjectEnvironmentCollection"/> (<c>DisableParallelization = true</c>) keeps this class from
/// running concurrently with anything else that touches the process-global <see cref="Logs"/> state.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class TileMapDepthSettingsLoadWarningsTests
{
    private const string LayerName = "Render_0";

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static (TileMapDepthSettings settings, List<string> warnings) LoadWithWarnings(
        Dictionary<string, string> customProperties)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var settings = TileMapDepthSettings.FromCustomProperties(customProperties, TileMapDepthRole.Ground, LayerName);
            return (settings, logger.Warnings);
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }
    }

    [Fact]
    public void FromCustomProperties_UnrecognizedDepthKey_WarnsOnceAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            ["depth.roleTypo"] = "Ground",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains("depth.roleTypo", warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).Role, settings.Role);
    }

    [Fact]
    public void FromCustomProperties_EnumKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.RoleKey] = "NotARole",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.RoleKey, warning);
        Assert.Equal(TileMapDepthRole.Ground, settings.Role);
    }

    [Fact]
    public void FromCustomProperties_RenderPassKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.RenderPassKey] = "NotAPass",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.RenderPassKey, warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).RenderPass, settings.RenderPass);
    }

    [Fact]
    public void FromCustomProperties_SortModeKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.SortModeKey] = "NotASortMode",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.SortModeKey, warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).SortMode, settings.SortMode);
    }

    [Fact]
    public void FromCustomProperties_OrderInLayerKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.OrderInLayerKey] = "notAnInt",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.OrderInLayerKey, warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).OrderInLayer, settings.OrderInLayer);
    }

    [Fact]
    public void FromCustomProperties_ElevationKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.ElevationKey] = "notAnInt",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.ElevationKey, warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).Elevation, settings.Elevation);
    }

    [Fact]
    public void FromCustomProperties_LocalSortOffsetKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.LocalSortOffsetKey] = "notAnInt",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.LocalSortOffsetKey, warning);
        Assert.Equal(TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).LocalSortOffset, settings.LocalSortOffset);
    }

    [Fact]
    public void FromCustomProperties_YSortKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.YSortKey] = "notABool",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.YSortKey, warning);
        Assert.Equal(DepthSortMode2D.None, settings.SortMode);
    }

    [Fact]
    public void FromCustomProperties_SpawnAsEntityKeyWithInvalidValue_WarnsAndDefaultApplies()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.SpawnAsEntityKey] = "notABool",
        });

        var warning = Assert.Single(warnings);
        Assert.Contains(LayerName, warning);
        Assert.Contains(TileMapDepthSettings.SpawnAsEntityKey, warning);
        Assert.Equal(
            TileMapDepthSettings.CreateDefault(TileMapDepthRole.Ground).SpawnAsEntity,
            settings.SpawnAsEntity);
    }

    [Fact]
    public void FromCustomProperties_SortingLayerKeyWithNonIntegerName_NeverWarns()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.SortingLayerKey] = "Props",
        });

        Assert.Empty(warnings);
        Assert.Equal(TileMapDepthSettings.GetStableSortingLayerId("Props"), settings.SortingLayer);
    }

    [Fact]
    public void FromCustomProperties_OnlyValidKeys_NeverWarnsAndMatchesTodaysDefaults()
    {
        var (settings, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.RoleKey] = "YSortedSource",
            [TileMapDepthSettings.RenderPassKey] = "YSortedWorld",
            [TileMapDepthSettings.SortingLayerKey] = "Props",
            [TileMapDepthSettings.OrderInLayerKey] = "12",
            [TileMapDepthSettings.ElevationKey] = "2",
            [TileMapDepthSettings.SortAnchorKey] = "16,48",
            [TileMapDepthSettings.LocalSortOffsetKey] = "-3",
            [TileMapDepthSettings.SortModeKey] = "TopDownYUp",
        });

        Assert.Empty(warnings);

        var expected = TileMapDepthSettings.FromCustomProperties(
            new Dictionary<string, string>
            {
                [TileMapDepthSettings.RoleKey] = "YSortedSource",
                [TileMapDepthSettings.RenderPassKey] = "YSortedWorld",
                [TileMapDepthSettings.SortingLayerKey] = "Props",
                [TileMapDepthSettings.OrderInLayerKey] = "12",
                [TileMapDepthSettings.ElevationKey] = "2",
                [TileMapDepthSettings.SortAnchorKey] = "16,48",
                [TileMapDepthSettings.LocalSortOffsetKey] = "-3",
                [TileMapDepthSettings.SortModeKey] = "TopDownYUp",
            },
            TileMapDepthRole.Ground); // legacy overload: identical result, no name, no warnings.

        Assert.Equal(expected.Role, settings.Role);
        Assert.Equal(expected.RenderPass, settings.RenderPass);
        Assert.Equal(expected.SortingLayer, settings.SortingLayer);
        Assert.Equal(expected.OrderInLayer, settings.OrderInLayer);
        Assert.Equal(expected.Elevation, settings.Elevation);
        Assert.Equal(expected.SortAnchor, settings.SortAnchor);
        Assert.Equal(expected.LocalSortOffset, settings.LocalSortOffset);
        Assert.Equal(expected.SortMode, settings.SortMode);
    }

    [Fact]
    public void FromCustomProperties_SameBadKeySameLayerLoadedOnce_WarnsExactlyOnce()
    {
        var (_, warnings) = LoadWithWarnings(new Dictionary<string, string>
        {
            [TileMapDepthSettings.OrderInLayerKey] = "notAnInt",
        });

        Assert.Single(warnings);
    }
}
