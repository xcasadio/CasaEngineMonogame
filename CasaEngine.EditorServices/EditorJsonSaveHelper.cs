using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

internal static class EditorJsonSaveHelper
{
    public static void SaveObjectBase(ObjectBase objectBase, JObject jObject)
    {
        jObject.Add("id", objectBase.Id.ToString());
        jObject.Add("name", objectBase.Name);
    }

    public static void SaveAssetInfo(AssetInfo assetInfo, JObject jObject)
    {
        jObject.Add("id", assetInfo.Id);
        jObject.Add("name", assetInfo.Name);
        jObject.Add("file_name", assetInfo.FileName);
        jObject.Add("asset_type", string.IsNullOrWhiteSpace(assetInfo.AssetType)
            ? AssetInfo.InferAssetType(assetInfo.FileName)
            : assetInfo.AssetType);
    }

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

    public static void Save(this LocalTransform localTransform, JObject jObject)
    {
        var positionObject = new JObject();
        localTransform.Position.Save(positionObject);
        jObject.Add("position", positionObject);

        var scaleObject = new JObject();
        localTransform.Scale.Save(scaleObject);
        jObject.Add("scale", scaleObject);

        var rotationObject = new JObject();
        localTransform.Orientation.Save(rotationObject);
        jObject.Add("rotation", rotationObject);
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