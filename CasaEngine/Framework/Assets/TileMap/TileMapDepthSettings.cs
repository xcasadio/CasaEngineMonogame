using System.Globalization;
using CasaEngine.Core.Logging;
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
        => FromCustomProperties(customProperties, defaultRole, layerName: null);

    /// <summary>
    /// Same as <see cref="FromCustomProperties(IReadOnlyDictionary{string,string},TileMapDepthRole)"/>,
    /// additionally reporting <c>depth.*</c> properties the loader does not understand. T3.1 (D4,
    /// narrowed): a key outside the recognised set, or a recognised key whose value fails to parse,
    /// logs one <see cref="Logs.WriteWarning"/> naming <paramref name="layerName"/> and the key and
    /// falls back to the current default - it never fails the load. <paramref name="layerName"/> is
    /// <c>null</c> for the legacy overload above, which stays silent since it has no layer name to
    /// report. <see cref="SortingLayerKey"/> is exempt by design: <see cref="ReadSortingLayer"/> hashes
    /// any non-integer string, so no string value is ever invalid for it.
    /// </summary>
    public static TileMapDepthSettings FromCustomProperties(
        IReadOnlyDictionary<string, string> customProperties,
        TileMapDepthRole defaultRole,
        string layerName)
    {
        var role = ReadEnum(customProperties, RoleKey, defaultRole, layerName);
        var defaults = CreateDefault(role);
        var renderPass = ReadEnum(customProperties, RenderPassKey, defaults.RenderPass, layerName);
        var sortingLayer = ReadSortingLayer(customProperties, defaults.SortingLayer);
        var orderInLayer = ReadInt32(customProperties, OrderInLayerKey, defaults.OrderInLayer, layerName);
        var elevation = ReadInt32(customProperties, ElevationKey, defaults.Elevation, layerName);
        var sortAnchor = ReadSortAnchor(customProperties, defaults.SortAnchor);
        var localSortOffset = ReadInt32(customProperties, LocalSortOffsetKey, defaults.LocalSortOffset, layerName);
        var sortMode = ReadEnum(customProperties, SortModeKey, defaults.SortMode, layerName);
        var spawnAsEntity = ReadBoolean(customProperties, SpawnAsEntityKey, defaults.SpawnAsEntity, layerName);

        if (sortMode == DepthSortMode2D.None && ReadBoolean(customProperties, YSortKey, false, layerName))
        {
            sortMode = DepthSortMode2D.TopDownYUp;
        }

        if (layerName != null)
        {
            WarnAboutUnrecognizedKeys(customProperties, layerName);
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

    /// <summary>The full set of "depth." keys this class reads. Anything else with that prefix warns.</summary>
    private static readonly HashSet<string> RecognizedKeys = new(StringComparer.Ordinal)
    {
        RoleKey, RenderPassKey, SortingLayerKey, OrderInLayerKey, ElevationKey,
        SortAnchorKey, SortAnchorXKey, SortAnchorYKey, LocalSortOffsetKey, SortModeKey,
        YSortKey, SpawnAsEntityKey
    };

    private const string DepthKeyPrefix = "depth.";

    private static void WarnAboutUnrecognizedKeys(IReadOnlyDictionary<string, string> customProperties, string layerName)
    {
        foreach (var key in customProperties.Keys)
        {
            if (key.StartsWith(DepthKeyPrefix, StringComparison.Ordinal) && !RecognizedKeys.Contains(key))
            {
                Logs.WriteWarning(
                    $"TileMap layer '{layerName}' has an unrecognized depth property '{key}'; it is ignored.");
            }
        }
    }

    private static void WarnInvalidValue(string layerName, string key)
    {
        if (layerName == null)
        {
            return;
        }

        Logs.WriteWarning(
            $"TileMap layer '{layerName}' has an invalid value for depth property '{key}'; the default is used.");
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

    private static int ReadInt32(IReadOnlyDictionary<string, string> customProperties, string key, int defaultValue, string layerName)
    {
        if (!TryGetValue(customProperties, key, out var value))
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        WarnInvalidValue(layerName, key);
        return defaultValue;
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, string> customProperties, string key, bool defaultValue, string layerName)
    {
        if (!TryGetValue(customProperties, key, out var value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        switch (value)
        {
            case "1":
                return true;
            case "0":
                return false;
        }

        WarnInvalidValue(layerName, key);
        return defaultValue;
    }

    private static TEnum ReadEnum<TEnum>(IReadOnlyDictionary<string, string> customProperties, string key, TEnum defaultValue, string layerName)
        where TEnum : struct, Enum
    {
        if (!TryGetValue(customProperties, key, out var value))
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            if (Enum.IsDefined(typeof(TEnum), numericValue))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
            }

            WarnInvalidValue(layerName, key);
            return defaultValue;
        }

        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        WarnInvalidValue(layerName, key);
        return defaultValue;
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
