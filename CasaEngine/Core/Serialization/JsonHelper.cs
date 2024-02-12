using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;

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

    public static IEnumerable<T> GetElements<T>(this JToken element, string arrayName, Func<JToken, T> loadElementFunc)
    {
        return element[arrayName].Select(loadElementFunc);
    }

    public static T GetEnum<T>(this JToken element) where T : struct
    {
        return Enum.Parse<T>(element.GetString(), true);
    }

    //================================ Save ======================================
    public static void Save(this Rectangle obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("w", obj.Width);
        jObject.Add("h", obj.Height);
    }

    public static void Save(this Point obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
    }

    public static void Save(this Size obj, JObject jObject)
    {
        jObject.Add("w", obj.Width);
        jObject.Add("h", obj.Height);
    }

    public static void Save(this Vector2 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
    }

    public static void Save(this Vector3 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
    }

    public static void Save(this Vector4 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
        jObject.Add("w", obj.W);
    }

    public static void Save(this Quaternion obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
        jObject.Add("w", obj.W);
    }

    public static void Save(this Color obj, JObject jObject)
    {
        jObject.Add("r", obj.R);
        jObject.Add("g", obj.G);
        jObject.Add("b", obj.B);
        jObject.Add("a", obj.A);
    }

    public static void Save(this Matrix obj, JObject jObject)
    {
        jObject.Add("_11", obj.M11);
        jObject.Add("_12", obj.M12);
        jObject.Add("_13", obj.M13);
        jObject.Add("_14", obj.M14);
        jObject.Add("_21", obj.M21);
        jObject.Add("_22", obj.M22);
        jObject.Add("_23", obj.M23);
        jObject.Add("_24", obj.M24);
        jObject.Add("_31", obj.M31);
        jObject.Add("_32", obj.M32);
        jObject.Add("_33", obj.M33);
        jObject.Add("_34", obj.M34);
        jObject.Add("_41", obj.M41);
        jObject.Add("_42", obj.M42);
        jObject.Add("_43", obj.M43);
        jObject.Add("_44", obj.M44);
    }
    public static void Save(this Viewport obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("w", obj.Width);
        jObject.Add("h", obj.Height);
        jObject.Add("min_depth", obj.MinDepth);
        jObject.Add("max_depth", obj.MaxDepth);
    }

    public static void Save(this VertexPositionNormalTexture obj, JObject jObject)
    {
        var positionObject = new JObject();
        obj.Position.Save(positionObject);
        jObject.Add("position", positionObject);

        var normalObject = new JObject();
        obj.Normal.Save(normalObject);
        jObject.Add("normal", normalObject);

        var textureCoordinateObject = new JObject();
        obj.TextureCoordinate.Save(textureCoordinateObject);
        jObject.Add("texture_coordinate", textureCoordinateObject);
    }

    public static void Save(this SamplerState samplerState, JObject jObject)
    {
        jObject.Add("texture_filter", samplerState.Filter.ConvertToString());
        jObject.Add("address_u", samplerState.AddressU.ConvertToString());
        jObject.Add("address_v", samplerState.AddressV.ConvertToString());
        jObject.Add("address_w", samplerState.AddressW.ConvertToString());
        var newNode = new JObject();
        samplerState.BorderColor.Save(newNode);
        jObject.Add("border_color", newNode);
        jObject.Add("max_anisotropy", samplerState.MaxAnisotropy);
        jObject.Add("max_mip_level", samplerState.MaxMipLevel);
        jObject.Add("mip_map_level_of_detail_bias", samplerState.MipMapLevelOfDetailBias);
        jObject.Add("comparison_function", samplerState.ComparisonFunction.ConvertToString());
        jObject.Add("filter_mode", samplerState.FilterMode.ConvertToString());
    }

    public static void AddArray<T>(this JObject jObject, string arrayName, IEnumerable<T> elements, Action<T, JObject> saveFunc)
    {
        var jArray = new JArray();
        jObject.Add(arrayName, jArray);

        foreach (var element in elements)
        {
            var newObject = new JObject();
            saveFunc(element, newObject);
            jArray.Add(newObject);
        }
    }

    public static void AddArray<T>(this JObject jObject, string arrayName, IEnumerable<T> elements)
    {
        var jArray = new JArray();
        jObject.Add(arrayName, jArray);

        foreach (var element in elements)
        {
            jArray.Add(element);
        }
    }

    public static string? ConvertToString(this Enum value)
    {
        return Enum.GetName(value.GetType(), value);
    }
}