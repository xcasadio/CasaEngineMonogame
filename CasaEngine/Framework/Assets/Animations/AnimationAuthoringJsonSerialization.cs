using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

internal static class AnimationAuthoringJsonSerialization
{
    public static JObject SaveVector3(Vector3 value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
        };
    }

    public static JObject SaveQuaternion(Quaternion value)
    {
        return new JObject
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z,
            ["w"] = value.W,
        };
    }

    public static JObject SaveBoneTransform(BoneTransform value)
    {
        return new JObject
        {
            ["translation"] = SaveVector3(value.Translation),
            ["rotation"] = SaveQuaternion(value.Rotation),
            ["scale"] = SaveVector3(value.Scale),
        };
    }

    public static BoneTransform LoadBoneTransform(JToken token)
    {
        if (token is not JObject node)
        {
            return BoneTransform.Identity;
        }

        var translation = node["translation"] is { } translationToken
            ? translationToken.GetVector3()
            : Vector3.Zero;
        var rotation = node["rotation"] is { } rotationToken
            ? rotationToken.GetQuaternion()
            : Quaternion.Identity;
        var scale = node["scale"] is { } scaleToken
            ? scaleToken.GetVector3()
            : Vector3.One;

        return new BoneTransform(translation, rotation, scale);
    }

    public static JObject SaveMatrix(Matrix value)
    {
        return new JObject
        {
            ["m11"] = value.M11,
            ["m12"] = value.M12,
            ["m13"] = value.M13,
            ["m14"] = value.M14,
            ["m21"] = value.M21,
            ["m22"] = value.M22,
            ["m23"] = value.M23,
            ["m24"] = value.M24,
            ["m31"] = value.M31,
            ["m32"] = value.M32,
            ["m33"] = value.M33,
            ["m34"] = value.M34,
            ["m41"] = value.M41,
            ["m42"] = value.M42,
            ["m43"] = value.M43,
            ["m44"] = value.M44,
        };
    }

    public static Matrix LoadMatrix(JToken token)
    {
        if (token is not JObject node)
        {
            return Matrix.Identity;
        }

        var matrix = Matrix.Identity;
        matrix.M11 = node["m11"]?.GetSingle() ?? Matrix.Identity.M11;
        matrix.M12 = node["m12"]?.GetSingle() ?? Matrix.Identity.M12;
        matrix.M13 = node["m13"]?.GetSingle() ?? Matrix.Identity.M13;
        matrix.M14 = node["m14"]?.GetSingle() ?? Matrix.Identity.M14;
        matrix.M21 = node["m21"]?.GetSingle() ?? Matrix.Identity.M21;
        matrix.M22 = node["m22"]?.GetSingle() ?? Matrix.Identity.M22;
        matrix.M23 = node["m23"]?.GetSingle() ?? Matrix.Identity.M23;
        matrix.M24 = node["m24"]?.GetSingle() ?? Matrix.Identity.M24;
        matrix.M31 = node["m31"]?.GetSingle() ?? Matrix.Identity.M31;
        matrix.M32 = node["m32"]?.GetSingle() ?? Matrix.Identity.M32;
        matrix.M33 = node["m33"]?.GetSingle() ?? Matrix.Identity.M33;
        matrix.M34 = node["m34"]?.GetSingle() ?? Matrix.Identity.M34;
        matrix.M41 = node["m41"]?.GetSingle() ?? Matrix.Identity.M41;
        matrix.M42 = node["m42"]?.GetSingle() ?? Matrix.Identity.M42;
        matrix.M43 = node["m43"]?.GetSingle() ?? Matrix.Identity.M43;
        matrix.M44 = node["m44"]?.GetSingle() ?? Matrix.Identity.M44;
        return matrix;
    }
}