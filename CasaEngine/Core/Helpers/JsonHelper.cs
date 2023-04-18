using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Helpers;

public static class JsonHelper
{
    public static JsonProperty GetJsonPropertyByName(this JsonElement jsonElement, string key)
    {
        return jsonElement.EnumerateObject().First(x => x.Name == key);
    }

    //Load
    public static Rectangle GetRectangle(this JsonElement element)
    {
        return new Rectangle
        {
            X = element.GetProperty("x").GetInt32(),
            Y = element.GetProperty("y").GetInt32(),
            Width = element.GetProperty("w").GetInt32(),
            Height = element.GetProperty("h").GetInt32()
        };
    }
    public static Point GetPoint(this JsonElement element)
    {
        return new Point
        {
            X = element.GetProperty("x").GetInt32(),
            Y = element.GetProperty("y").GetInt32()
        };
    }

    public static Vector2 GetVector2(this JsonElement element)
    {
        return new Vector2
        {
            X = element.GetProperty("x").GetSingle(),
            Y = element.GetProperty("y").GetSingle()
        };
    }

    public static Vector3 GetVector3(this JsonElement element)
    {
        return new Vector3
        {
            X = element.GetProperty("x").GetSingle(),
            Y = element.GetProperty("y").GetSingle(),
            Z = element.GetProperty("z").GetSingle()
        };
    }

    public static Vector4 GetVector4(this JsonElement element)
    {
        return new Vector4
        {
            X = element.GetProperty("x").GetSingle(),
            Y = element.GetProperty("y").GetSingle(),
            Z = element.GetProperty("z").GetSingle(),
            W = element.GetProperty("w").GetSingle()
        };
    }

    public static Quaternion GetQuaternion(this JsonElement element)
    {
        return new Quaternion
        {
            X = element.GetProperty("x").GetSingle(),
            Y = element.GetProperty("y").GetSingle(),
            Z = element.GetProperty("z").GetSingle(),
            W = element.GetProperty("w").GetSingle()
        };
    }

    public static Viewport GetViewPort(this JsonElement element)
    {
        var viewport = new Viewport();
        viewport.X = element.GetProperty("x").GetInt32();
        viewport.Y = element.GetProperty("y").GetInt32();
        viewport.Width = element.GetProperty("width").GetInt32();
        viewport.Height = element.GetProperty("height").GetInt32();
        viewport.MinDepth = element.GetProperty("min_depth").GetSingle();
        viewport.MaxDepth = element.GetProperty("max_depth").GetSingle();
        return viewport;
    }

    public static VertexPositionNormalTexture GetVertexPositionNormalTexture(this JsonElement element)
    {
        var obj = new VertexPositionNormalTexture();
        obj.Position = element.GetProperty("position").GetVector3();
        obj.Normal = element.GetProperty("normal").GetVector3();
        obj.TextureCoordinate = element.GetProperty("texture_coordinate").GetVector2();
        return obj;
    }

    public static T GetEnum<T>(this JsonElement element) where T : struct
    {
        return Enum.Parse<T>(element.GetString(), true);
    }

    //Save
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
        jObject.Add("width", obj.Width);
        jObject.Add("height", obj.Height);
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

    public static string? ConvertToString(this Enum value)
    {
        return Enum.GetName(value.GetType(), value);
    }
}