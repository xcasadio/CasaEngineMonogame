using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

/// <summary>
/// ViewModel for <see cref="UnlitTextureMaterial"/>. Exposes albedo texture, tint color and alpha.
/// </summary>
public class UnlitTextureMaterialViewModel : MaterialViewModel
{
    private readonly UnlitTextureMaterial _unlit;

    public Guid AlbedoAssetId
    {
        get => _unlit.AlbedoAssetId;
        set { _unlit.AlbedoAssetId = value; OnPropertyChanged(); }
    }

    public Color Tint
    {
        get => _unlit.Tint;
        set { _unlit.Tint = value; OnPropertyChanged(); }
    }

    public float Alpha
    {
        get => _unlit.Alpha;
        set { _unlit.Alpha = value; OnPropertyChanged(); }
    }

    public UnlitTextureMaterialViewModel(UnlitTextureMaterial material) : base(material)
    {
        _unlit = material;
    }

    protected override void ReloadTextures()
    {
        if (ContentManager == null) return;
        if (_unlit.AlbedoAssetId != Guid.Empty)
        {
            var tex = ContentManager.Load<Texture>(_unlit.AlbedoAssetId);
            _unlit.Albedo = tex?.Resource;
        }
        else
        {
            _unlit.Albedo = null;
        }
    }
}
