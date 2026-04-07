using CasaEngine.Core.Log;
using CasaEngine.Framework.Materials;
using Newtonsoft.Json.Linq;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Loads a <see cref="MaterialBase"/> sub-class from a <c>.material</c> JSON file.
/// The JSON must contain a <c>"type"</c> field that names the concrete class.
/// </summary>
public class MaterialLoader : IAssetLoader
{
    public bool IsFileSupported(string fileName) =>
        Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.Material, StringComparison.OrdinalIgnoreCase);

    public object? LoadAsset(string fileName, AssetContentManager assetContentManager)
    {
        try
        {
            var jsonText = File.ReadAllText(fileName);
            var jObj = JObject.Parse(jsonText);

            var typeName = jObj["type"]?.Value<string>();
            MaterialBase material = typeName switch
            {
                nameof(UnlitTextureMaterial) => new UnlitTextureMaterial(),
                nameof(LitDiffuseMaterial)   => new LitDiffuseMaterial(),
                nameof(Material)             => new Material(),
                _                            => CreateFallback(typeName)
            };

            material.Load(jObj);

            // Resolve texture assets if material is of a known concrete type
            if (material is UnlitTextureMaterial unlit && unlit.BasColorAssetId != Guid.Empty)
            {
                var tex = LoadTextureAsset(unlit.BasColorAssetId, assetContentManager);
                unlit.BasColor = tex?.Resource;
            }
            else if (material is LitDiffuseMaterial lit)
            {
                if (lit.BasColorAssetId != Guid.Empty)
                {
                    var tex = LoadTextureAsset(lit.BasColorAssetId, assetContentManager);
                    lit.BasColor = tex?.Resource;
                }

                if (lit.NormalMapAssetId != Guid.Empty)
                {
                    var tex = LoadTextureAsset(lit.NormalMapAssetId, assetContentManager);
                    lit.NormalMap = tex?.Resource;
                }

                if (lit.ReflectionCubeAssetId != Guid.Empty)
                {
                    lit.ReflectionCube = LoadTextureCubeAsset(lit.ReflectionCubeAssetId, assetContentManager);
                }
            }
            else if (material is Material pbr)
            {
                pbr.LoadTextures(assetContentManager);
            }

            return material;
        }
        catch (Exception e)
        {
            Logs.WriteException(new Exception($"[MaterialLoader] Cannot load material '{fileName}'", e));
            return null;
        }
    }

    private static MaterialBase CreateFallback(string? typeName)
    {
        Logs.WriteWarning($"[MaterialLoader] Unknown material type '{typeName}'. Falling back to UnlitTextureMaterial.");
        return new UnlitTextureMaterial();
    }

    private static Textures.Texture? LoadTextureAsset(Guid assetId, AssetContentManager assetContentManager)
    {
        var texture = assetContentManager.Load<Textures.Texture>(assetId);
        texture?.Load(assetContentManager);
        return texture;
    }

    private static XnaTextureCube? LoadTextureCubeAsset(Guid assetId, AssetContentManager assetContentManager)
    {
        try
        {
            return assetContentManager.Load<XnaTextureCube>(assetId);
        }
        catch
        {
            return null;
        }
    }
}
