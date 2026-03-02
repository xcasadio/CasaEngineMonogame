using System;
using System.Collections.Generic;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Materials;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

/// <summary>
/// ViewModel wrapping a <see cref="MaterialBase"/> instance.
/// Exposes all common properties (render states, flags, queue) for WPF data binding.
/// Concrete sub-classes expose material-type-specific properties.
/// </summary>
public class MaterialViewModel : NotifyPropertyChangeBase
{
    protected readonly MaterialBase _material;

    // -------------------------------------------------------------------------
    // Static lists for ComboBox ItemsSources
    // -------------------------------------------------------------------------

    public static IReadOnlyList<string>     AvailableBlendStates        => MaterialBase.BlendStateNames;
    public static IReadOnlyList<string>     AvailableDepthStencilStates => MaterialBase.DepthStencilStateNames;
    public static IReadOnlyList<string>     AvailableRasterizerStates   => MaterialBase.RasterizerStateNames;
    public static IReadOnlyList<string>     AvailableSamplerStates      => MaterialBase.SamplerStateNames;
    public static IReadOnlyList<RenderQueue> AvailableQueues => new List<RenderQueue>
    {
        RenderQueue.Opaque,
        RenderQueue.AlphaTest,
        RenderQueue.Transparent,
        RenderQueue.Overlay,
    };

    // -------------------------------------------------------------------------
    // Common MaterialBase properties
    // -------------------------------------------------------------------------

    public MaterialBase Material => _material;

    public string Name
    {
        get => _material.Name;
        set { _material.Name = value; OnPropertyChanged(); }
    }

    public bool IsTransparent
    {
        get => _material.IsTransparent;
        set { _material.IsTransparent = value; OnPropertyChanged(); }
    }

    public RenderQueue Queue
    {
        get => _material.Queue;
        set { _material.Queue = value; OnPropertyChanged(); }
    }

    public bool CastShadows
    {
        get => _material.CastShadows;
        set { _material.CastShadows = value; OnPropertyChanged(); }
    }

    public bool ReceiveShadows
    {
        get => _material.ReceiveShadows;
        set { _material.ReceiveShadows = value; OnPropertyChanged(); }
    }

    public Guid ShaderAssetId
    {
        get => _material.ShaderAssetId;
        set { _material.ShaderAssetId = value; OnPropertyChanged(); }
    }

    // -------------------------------------------------------------------------
    // Render states exposed as string keys
    // -------------------------------------------------------------------------

    public string BlendStateName
    {
        get => _material.GetBlendStateName();
        set { _material.SetBlendStateByName(value); OnPropertyChanged(); }
    }

    public string DepthStencilStateName
    {
        get => _material.GetDepthStateName();
        set { _material.SetDepthStateByName(value); OnPropertyChanged(); }
    }

    public string RasterizerStateName
    {
        get => _material.GetRasterizerStateName();
        set { _material.SetRasterizerStateByName(value); OnPropertyChanged(); }
    }

    public string SamplerStateName
    {
        get => _material.GetSamplerStateName();
        set { _material.SetSamplerStateByName(value); OnPropertyChanged(); }
    }

    // -------------------------------------------------------------------------
    // Type display
    // -------------------------------------------------------------------------

    public string MaterialTypeName => _material.GetType().Name;

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes the material to its <c>.material</c> JSON file via <see cref="AssetSaver"/>.
    /// The file path is resolved from <see cref="AssetCatalog"/> using the material's <see cref="MaterialBase.Id"/>.
    /// </summary>
    public void SaveMaterial()
    {
        var assetInfo = AssetCatalog.Get(_material.Id);
        if (assetInfo == null)
        {
            Logs.WriteWarning($"[MaterialViewModel] Cannot save material '{_material.Name}': not found in AssetCatalog (Id={_material.Id}).");
            return;
        }

        AssetSaver.SaveAsset(assetInfo.FileName, _material);
    }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public MaterialViewModel(MaterialBase material)
    {
        _material = material ?? throw new ArgumentNullException(nameof(material));
    }
}
