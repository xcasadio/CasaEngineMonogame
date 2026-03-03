using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

/// <summary>
/// ViewModel for <see cref="LitDiffuseMaterial"/>. Exposes BasColor, normal map,
/// diffuse/emissive/specular colors and specular power.
/// </summary>
public class LitDiffuseMaterialViewModel : MaterialViewModel
{
    private readonly LitDiffuseMaterial _lit;

    public Guid BasColorAssetId
    {
        get => _lit.BasColorAssetId;
        set { _lit.BasColorAssetId = value; OnPropertyChanged(); }
    }

    public Guid NormalMapAssetId
    {
        get => _lit.NormalMapAssetId;
        set { _lit.NormalMapAssetId = value; OnPropertyChanged(); }
    }

    public Color DiffuseColor
    {
        get => _lit.DiffuseColor;
        set { _lit.DiffuseColor = value; OnPropertyChanged(); }
    }

    public Vector3 EmissiveColor
    {
        get => _lit.EmissiveColor;
        set { _lit.EmissiveColor = value; OnPropertyChanged(); }
    }

    public Vector3 SpecularColor
    {
        get => _lit.SpecularColor;
        set { _lit.SpecularColor = value; OnPropertyChanged(); }
    }

    public float SpecularPower
    {
        get => _lit.SpecularPower;
        set { _lit.SpecularPower = value; OnPropertyChanged(); }
    }

    public LitDiffuseMaterialViewModel(LitDiffuseMaterial material) : base(material)
    {
        _lit = material;
    }

    protected override void ReloadTextures()
    {
        if (ContentManager == null) return;
        if (_lit.BasColorAssetId != Guid.Empty)
        {
            var tex = ContentManager.Load<Texture>(_lit.BasColorAssetId);
            _lit.BasColor = tex?.Resource;
        }
        else
        {
            _lit.BasColor = null;
        }

        if (_lit.NormalMapAssetId != Guid.Empty)
        {
            var tex = ContentManager.Load<Texture>(_lit.NormalMapAssetId);
            _lit.NormalMap = tex?.Resource;
        }
        else
        {
            _lit.NormalMap = null;
        }
    }
}
