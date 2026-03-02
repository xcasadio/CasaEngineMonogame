using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

/// <summary>
/// ViewModel for <see cref="Material"/> (PBR multi-channel with 8 texture slots).
/// </summary>
public class PbrMaterialViewModel : MaterialViewModel
{
    private readonly Material _pbr;

    public Guid TextureBaseColorAssetId
    {
        get => _pbr.TextureBaseColorAssetId;
        set { _pbr.TextureBaseColorAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureOpacityAssetId
    {
        get => _pbr.TextureOpacityAssetId;
        set { _pbr.TextureOpacityAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureNormalAssetId
    {
        get => _pbr.TextureNormalAssetId;
        set { _pbr.TextureNormalAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureSpecularAssetId
    {
        get => _pbr.TextureSpecularAssetId;
        set { _pbr.TextureSpecularAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureRoughnessAssetId
    {
        get => _pbr.TextureRoughnessAssetId;
        set { _pbr.TextureRoughnessAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureTangentAssetId
    {
        get => _pbr.TextureTangentAssetId;
        set { _pbr.TextureTangentAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureHeightAssetId
    {
        get => _pbr.TextureHeightAssetId;
        set { _pbr.TextureHeightAssetId = value; OnPropertyChanged(); }
    }

    public Guid TextureReflectionAssetId
    {
        get => _pbr.TextureReflectionAssetId;
        set { _pbr.TextureReflectionAssetId = value; OnPropertyChanged(); }
    }

    public PbrMaterialViewModel(Material material) : base(material)
    {
        _pbr = material;
    }

    protected override void ReloadTextures()
    {
        if (ContentManager == null) return;
        _pbr.LoadTextures(ContentManager);
    }
}
