using System.Globalization;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.TileMap;

public readonly struct TileMapDepthSettings
{
    public const string RoleKey = "depth.role";
    public const string RenderPassKey = "depth.renderPass";
    public const string SortingLayerKey = "depth.sortingLayer";
    public const string OrderInLayerKey = "depth.orderInLayer";
    public const string ElevationKey = "depth.elevation";
    public const string SortAnchorKey = "depth.sortAnchor";
    public const string SortAnchorXKey = "depth.sortAnchorX";
    public const string SortAnchorYKey = "depth.sortAnchorY";
    public const string LocalSortOffsetKey = "depth.localSortOffset";
    public const string SortModeKey = "depth.sortMode";
    public const string YSortKey = "depth.ySort";
    public const string SpawnAsEntityKey = "depth.spawnAsEntity";

    public TileMapDepthSettings(
        TileMapDepthRole role,
        RenderPass2D renderPass,
        int sortingLayer,
        int orderInLayer,
        int elevation,
        Vector2 sortAnchor,
        int localSortOffset,
        DepthSortMode2D sortMode,
        bool spawnAsEntity)
    {
        Role = role;
        RenderPass = renderPass;
        SortingLayer = sortingLayer;
        OrderInLayer = orderInLayer;
        Elevation = elevation;
        SortAnchor = sortAnchor;
        LocalSortOffset = localSortOffset;
        SortMode = sortMode;
        SpawnAsEntity = spawnAsEntity;
    }

    public TileMapDepthRole Role { get; }

    public RenderPass2D RenderPass { get; }

    public int SortingLayer { get; }

    public int OrderInLayer { get; }

    public int Elevation { get; }

    public Vector2 SortAnchor { get; }

    public int LocalSortOffset { get; }

    public DepthSortMode2D SortMode { get; }

    public bool SpawnAsEntity { get; }

    public bool UsesDynamicSort => SortMode != DepthSortMode2D.None;

    public bool ShouldRenderTiles => Role is not TileMapDepthRole.CollisionOnly and not TileMapDepthRole.ObjectSource;

    public bool KeepsStaticChunking => Role is TileMapDepthRole.Background
        or TileMapDepthRole.Ground
        or TileMapDepthRole.GroundDetails
        or TileMapDepthRole.Foreground
        or TileMapDepthRole.Debug;

    public bool EmitsSortableObjects => SpawnAsEntity || Role is TileMapDepthRole.YSortedSource or TileMapDepthRole.ObjectSource;

    public static TileMapDepthSettings CreateDefault(TileMapDepthRole role)
    {
        var renderPass = GetDefaultRenderPass(role);
        var sortMode = role is TileMapDepthRole.YSortedSource or TileMapDepthRole.ObjectSource
            ? DepthSortMode2D.TopDownYUp
            : DepthSortMode2D.None;

        return new TileMapDepthSettings(
            role,
            renderPass,
            (int)renderPass,
            0,
            0,
            Vector2.Zero,
            0,
            sortMode,
            role == TileMapDepthRole.ObjectSource);
    }

    public static TileMapDepthSettings FromCustomProperties(
        IReadOnlyDictionary<string, string> customProperties,
        TileMapDepthRole defaultRole)
    {
        var role = ReadEnum(customProperties, RoleKey, defaultRole);
        var defaults = CreateDefault(role);
        var renderPass = ReadEnum(customProperties, RenderPassKey, defaults.RenderPass);
        var sortingLayer = ReadSortingLayer(customProperties, defaults.SortingLayer);
        var orderInLayer = ReadInt32(customProperties, OrderInLayerKey, defaults.OrderInLayer);
        var elevation = ReadInt32(customProperties, ElevationKey, defaults.Elevation);
        var sortAnchor = ReadSortAnchor(customProperties, defaults.SortAnchor);
        var localSortOffset = ReadInt32(customProperties, LocalSortOffsetKey, defaults.LocalSortOffset);
        var sortMode = ReadEnum(customProperties, SortModeKey, defaults.SortMode);
        var spawnAsEntity = ReadBoolean(customProperties, SpawnAsEntityKey, defaults.SpawnAsEntity);

        if (sortMode == DepthSortMode2D.None && ReadBoolean(customProperties, YSortKey, false))
        {
            sortMode = DepthSortMode2D.TopDownYUp;
        }

        return new TileMapDepthSettings(
            role,
            renderPass,
            sortingLayer,
            orderInLayer,
            elevation,
            sortAnchor,
            localSortOffset,
            sortMode,
            spawnAsEntity);
    }

    public static int GetStableSortingLayerId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        unchecked
        {
            const int offset = unchecked((int)2166136261);
            const int prime = 16777619;
            var hash = offset;

            for (var index = 0; index < name.Length; index++)
            {
                hash ^= char.ToUpperInvariant(name[index]);
                hash *= prime;
            }

            return hash & 0x7fffffff;
        }
    }

    private static RenderPass2D GetDefaultRenderPass(TileMapDepthRole role)
        => role switch
        {
            TileMapDepthRole.Background => RenderPass2D.Background,
            TileMapDepthRole.Ground => RenderPass2D.Ground,
            TileMapDepthRole.GroundDetails => RenderPass2D.GroundDetails,
            TileMapDepthRole.YSortedSource => RenderPass2D.YSortedWorld,
            TileMapDepthRole.Foreground => RenderPass2D.Foreground,
            TileMapDepthRole.CollisionOnly => RenderPass2D.Ground,
            TileMapDepthRole.ObjectSource => RenderPass2D.YSortedWorld,
            TileMapDepthRole.Debug => RenderPass2D.Effects,
            _ => RenderPass2D.Ground
        };

    private static int ReadSortingLayer(IReadOnlyDictionary<string, string> customProperties, int defaultValue)
    {
        if (!TryGetValue(customProperties, SortingLayerKey, out var value))
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
            ? intValue
            : GetStableSortingLayerId(value);
    }

    private static Vector2 ReadSortAnchor(IReadOnlyDictionary<string, string> customProperties, Vector2 defaultValue)
    {
        var sortAnchor = defaultValue;
        if (TryGetValue(customProperties, SortAnchorKey, out var combinedValue))
        {
            var separatorIndex = combinedValue.IndexOf(',');
            if (separatorIndex > 0 && separatorIndex < combinedValue.Length - 1)
            {
                var xToken = combinedValue[..separatorIndex].Trim();
                var yToken = combinedValue[(separatorIndex + 1)..].Trim();
                if (float.TryParse(xToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var combinedX)
                    && float.TryParse(yToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var combinedY))
                {
                    sortAnchor = new Vector2(combinedX, combinedY);
                }
            }
        }

        if (TryGetValue(customProperties, SortAnchorXKey, out var xValue)
            && float.TryParse(xValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
        {
            sortAnchor.X = x;
        }

        if (TryGetValue(customProperties, SortAnchorYKey, out var yValue)
            && float.TryParse(yValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            sortAnchor.Y = y;
        }

        return sortAnchor;
    }

    private static int ReadInt32(IReadOnlyDictionary<string, string> customProperties, string key, int defaultValue)
        => TryGetValue(customProperties, key, out var value)
           && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;

    private static bool ReadBoolean(IReadOnlyDictionary<string, string> customProperties, string key, bool defaultValue)
    {
        if (!TryGetValue(customProperties, key, out var value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static TEnum ReadEnum<TEnum>(IReadOnlyDictionary<string, string> customProperties, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (!TryGetValue(customProperties, key, out var value))
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            return Enum.IsDefined(typeof(TEnum), numericValue)
                ? (TEnum)Enum.ToObject(typeof(TEnum), numericValue)
                : defaultValue;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result)
            ? result
            : defaultValue;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, string> customProperties, string key, out string value)
    {
        if (customProperties.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }
}
