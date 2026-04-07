using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Models;

public static class StaticModelMaterialResolver
{
    public static MaterialBase ResolveMeshMaterial(StaticModelMesh mesh, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (mesh.MaterialAssetId == Guid.Empty && mesh.Material != null)
        {
            return mesh.Material;
        }

        if (TryLoadMaterial(mesh.MaterialAssetId, assetContentManager, out var material))
        {
            return material;
        }

        if (TryLoadTexture(mesh, assetContentManager, out var textureResource))
        {
            return CreateTextureFallbackMaterial(GetSlotDisplayName(mesh.SlotName, mesh.Name), mesh.TextureAssetId, textureResource, mesh.Texture?.PreferredSamplerState);
        }

        return CreateMissingMaterial(GetSlotDisplayName(mesh.SlotName, mesh.Name));
    }

    public static MaterialBase ResolveSubMeshMaterial(StaticModelMesh mesh, SubMesh subMesh, AssetContentManager assetContentManager, MaterialBase meshMaterial)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(subMesh);
        ArgumentNullException.ThrowIfNull(assetContentManager);
        ArgumentNullException.ThrowIfNull(meshMaterial);

        if (subMesh.MaterialAssetId == Guid.Empty)
        {
            return subMesh.Material ?? meshMaterial;
        }

        if (TryLoadMaterial(subMesh.MaterialAssetId, assetContentManager, out var material))
        {
            return material;
        }

        if (TryLoadTexture(mesh, assetContentManager, out var textureResource))
        {
            return CreateTextureFallbackMaterial(GetSlotDisplayName(subMesh.SlotName, mesh.Name), mesh.TextureAssetId, textureResource, mesh.Texture?.PreferredSamplerState);
        }

        return CreateMissingMaterial(GetSlotDisplayName(subMesh.SlotName, mesh.Name));
    }

    public static LitDiffuseMaterial CreateTextureFallbackMaterial(string slotName, Guid textureAssetId, Texture2D? basColor, SamplerState? samplerState = null)
    {
        return new LitDiffuseMaterial
        {
            Name = $"{NormalizeSlotName(slotName)} [Generated Texture Material]",
            BasColorAssetId = textureAssetId,
            BasColor = basColor,
            SamplerState = samplerState,
            DiffuseColor = Color.White,
            EmissiveColor = Vector3.Zero,
            SpecularColor = new Vector3(0.5f),
            SpecularPower = 16.0f,
        };
    }

    public static LitDiffuseMaterial CreateMissingMaterial(string slotName)
    {
        return new LitDiffuseMaterial
        {
            Name = $"{NormalizeSlotName(slotName)} [Missing Material]",
            DiffuseColor = Color.Magenta,
            EmissiveColor = new Vector3(0.1f, 0.0f, 0.1f),
            SpecularColor = Vector3.Zero,
            SpecularPower = 1.0f,
        };
    }

    private static bool TryLoadMaterial(Guid materialAssetId, AssetContentManager assetContentManager, out MaterialBase material)
    {
        return MaterialRuntimeResolver.TryLoadRuntimeMaterial(materialAssetId, assetContentManager, out material);
    }

    private static bool TryLoadTexture(StaticModelMesh mesh, AssetContentManager assetContentManager, out Texture2D textureResource)
    {
        textureResource = null!;
        if (mesh.TextureAssetId == Guid.Empty)
        {
            return false;
        }

        try
        {
            if (mesh.Texture?.Resource == null)
            {
                mesh.LoadTexture(mesh.TextureAssetId, assetContentManager);
            }

            if (mesh.Texture?.Resource == null)
            {
                return false;
            }

            textureResource = mesh.Texture.Resource;
            return true;
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return false;
        }
    }

    private static string GetSlotDisplayName(string slotName, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            return slotName;
        }

        return NormalizeSlotName(fallbackName);
    }

    private static string NormalizeSlotName(string slotName)
        => string.IsNullOrWhiteSpace(slotName)
            ? "Slot"
            : slotName.Trim();
}