using System;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class StaticModelComponentControl : UserControl
{
    public StaticModelComponentControl()
    {
        InitializeComponent();
    }

    public bool ValidateStaticModelAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is StaticModelComponentViewModel viewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.StaticModel)
        {
            var staticModelComponent = viewModel.Component as StaticModelComponent;
            var assetContentManager = staticModelComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
            staticModelComponent.StaticModelAssetId = assetId;
            staticModelComponent.StaticModel = assetContentManager.Load<StaticModel>(assetId);
            staticModelComponent.StaticModel.Initialize(assetContentManager);
            return true;
        }

        return false;
    }
}
