using CasaEngine.Core.Math;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Math.Size;

namespace CasaEngine.Core.Serialization;

public static class JsonHelper
{
    //================================ Load ======================================
    public static string GetString(this JToken element)
    {
        return element.Value<string>();
    }

    public static Guid GetGuid(this JToken element)
    {
        return Guid.Parse(element.Value<string>());
    }

    public static float GetSingle(this JToken element)
    {
        return element.Value<float>();
    }

    public static int GetInt32(this JToken element)
    {
        return element.Value<int>();
    }

    public static uint GetUInt32(this JToken element)
    {
        return element.Value<uint>();
    }

    public static byte GetByte(this JToken element)
    {
        return element.Value<byte>();
    }

    public static bool GetBoolean(this JToken element)
    {
        return element.Value<bool>();
    }

    public static Rectangle GetRectangle(this JToken element)
    {
        return new Rectangle
        {
            X = element["x"].GetInt32(),
            Y = element["y"].GetInt32(),
            Width = element["w"].GetInt32(),
            Height = element["h"].GetInt32()
        };
    }

    public static Point GetPoint(this JToken element)
    {
        return new Point
        {
            X = element["x"].GetInt32(),
            Y = element["y"].GetInt32()
        };
    }

    public static Size GetSize(this JToken element)
    {
        return new Size
        {
            Width = element["w"].GetInt32(),
            Height = element["h"].GetInt32()
        };
    }

    public static Vector2 GetVector2(this JToken element)
    {
        return new Vector2
        {
            X = element["x"].GetSingle(),
            Y = element["y"].GetSingle()
        };
    }

    public static Vector3 GetVector3(this JToken element)
    {
        return new Vector3
        {
            X = element["x"].GetSingle(),
            Y = element["y"].GetSingle(),
            Z = element["z"].GetSingle()
        };
    }

    public static Vector4 GetVector4(this JToken element)
    {
        return new Vector4
        {
            X = element["x"].GetSingle(),
            Y = element["y"].GetSingle(),
            Z = element["z"].GetSingle(),
            W = element["w"].GetSingle()
        };
    }

    public static Quaternion GetQuaternion(this JToken element)
    {
        return new Quaternion
        {
            X = element["x"].GetSingle(),
            Y = element["y"].GetSingle(),
            Z = element["z"].GetSingle(),
            W = element["w"].GetSingle()
        };
    }

    public static Color GetColor(this JToken element)
    {
        return new Color
        {
            R = element["r"].GetByte(),
            G = element["g"].GetByte(),
            B = element["b"].GetByte(),
            A = element["a"].GetByte()
        };
    }

    public static Viewport GetViewPort(this JToken element)
    {
        var viewport = new Viewport();
        viewport.X = element["x"].GetInt32();
        viewport.Y = element["y"].GetInt32();
        viewport.Width = element["w"].GetInt32();
        viewport.Height = element["h"].GetInt32();
        viewport.MinDepth = element["min_depth"].GetSingle();
        viewport.MaxDepth = element["max_depth"].GetSingle();
        return viewport;
    }

    public static VertexPositionNormalTexture GetVertexPositionNormalTexture(this JToken element)
    {
        var obj = new VertexPositionNormalTexture();
        obj.Position = element["position"].GetVector3();
        obj.Normal = element["normal"].GetVector3();
        obj.TextureCoordinate = element["texture_coordinate"].GetVector2();
        return obj;
    }

    public static SamplerState GetSamplerState(this JToken element)
    {
        var samplerState = new SamplerState();
        samplerState.Filter = element["texture_filter"].GetEnum<TextureFilter>();
        samplerState.AddressU = element["address_u"].GetEnum<TextureAddressMode>();
        samplerState.AddressV = element["address_v"].GetEnum<TextureAddressMode>();
        samplerState.AddressW = element["address_w"].GetEnum<TextureAddressMode>();
        samplerState.BorderColor = element["border_color"].GetColor();
        samplerState.MaxAnisotropy = element["max_anisotropy"].GetInt32();
        samplerState.MaxMipLevel = element["max_mip_level"].GetInt32();
        samplerState.MipMapLevelOfDetailBias = element["mip_map_level_of_detail_bias"].GetSingle();
        samplerState.ComparisonFunction = element["comparison_function"].GetEnum<CompareFunction>();
        samplerState.FilterMode = element["filter_mode"].GetEnum<TextureFilterMode>();

        return samplerState;
    }

    public static Coordinates GetCoordinates(this JToken element)
    {
        var c = new Coordinates();
        c.Position    = element["position"].GetVector3();
        c.Scale       = element["scale"].GetVector3();
        c.Orientation = element["rotation"].GetQuaternion();
        return c;
    }

    public static IEnumerable<T> GetElements<T>(this JToken element, string arrayName, Func<JToken, T> loadElementFunc)
    {
        return element[arrayName].Select(loadElementFunc);
    }

    public static T GetEnum<T>(this JToken element) where T : struct
    {
        return Enum.Parse<T>(element.GetString(), true);
    }

}