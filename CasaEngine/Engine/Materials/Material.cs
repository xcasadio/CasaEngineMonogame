using System.Text.Json;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Materials;

public class Material
{
    public string TextureBaseColorName;
    public Texture2D TextureBaseColor;

    public string TextureOpacityName;
    public Texture2D TextureOpacityColor;

    public string TextureNormalName;
    public Texture2D TextureNormal;

    public string TextureSpecularName;
    public Texture2D TextureSpecular;

    public string TextureRoughnessName;
    public Texture2D TextureRoughness;

    public string TextureTangentName;
    public Texture2D TextureTangent;

    public string TextureHeightName;
    public Texture2D TextureHeight;

    public string TextureReflectionName;
    public Texture2D TextureReflection;

    public void Load(AssetContentManager content, GraphicsDevice graphicsDevice)
    {
        if (string.IsNullOrEmpty(TextureBaseColorName))
        {
            TextureBaseColor = content.Load<Texture2D>(TextureBaseColorName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureOpacityName))
        {
            TextureOpacityColor = content.Load<Texture2D>(TextureOpacityName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureNormalName))
        {
            TextureNormal = content.Load<Texture2D>(TextureNormalName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureSpecularName))
        {
            TextureSpecular = content.Load<Texture2D>(TextureSpecularName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureRoughnessName))
        {
            TextureRoughness = content.Load<Texture2D>(TextureRoughnessName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureTangentName))
        {
            TextureTangent = content.Load<Texture2D>(TextureTangentName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureHeightName))
        {
            TextureHeight = content.Load<Texture2D>(TextureHeightName, graphicsDevice);
        }

        if (string.IsNullOrEmpty(TextureReflectionName))
        {
            TextureReflection = content.Load<Texture2D>(TextureReflectionName, graphicsDevice);
        }
    }

    public void Load(JsonElement element)
    {
        TextureBaseColorName = element.GetProperty("texture_base_color_name").GetString();
        TextureOpacityName = element.GetProperty("texture_opacity_name").GetString();
        TextureNormalName = element.GetProperty("texture_normal_name").GetString();
        TextureSpecularName = element.GetProperty("texture_specular_name").GetString();
        TextureRoughnessName = element.GetProperty("texture_roughness_name").GetString();
        TextureTangentName = element.GetProperty("texture_tangent_name").GetString();
        TextureHeightName = element.GetProperty("texture_height_name").GetString();
        TextureReflectionName = element.GetProperty("texture_reflection_name").GetString();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("texture_base_color_name", TextureBaseColorName == null ? "null" : TextureBaseColorName);
        jObject.Add("texture_opacity_name", TextureOpacityName == null ? "null" : TextureOpacityName);
        jObject.Add("texture_normal_name", TextureNormalName == null ? "null" : TextureNormalName);
        jObject.Add("texture_specular_name", TextureSpecularName == null ? "null" : TextureSpecularName);
        jObject.Add("texture_roughness_name", TextureRoughnessName == null ? "null" : TextureRoughnessName);
        jObject.Add("texture_tangent_name", TextureTangentName == null ? "null" : TextureTangentName);
        jObject.Add("texture_height_name", TextureHeightName == null ? "null" : TextureHeightName);
        jObject.Add("texture_reflection_name", TextureReflectionName == null ? "null" : TextureReflectionName);
    }

#endif
}