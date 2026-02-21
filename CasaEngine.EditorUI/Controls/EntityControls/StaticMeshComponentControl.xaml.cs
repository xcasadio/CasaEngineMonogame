using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Graphics;
using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets.Textures;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class StaticMeshComponentControl : UserControl
{
    public StaticMeshComponentControl()
    {
        InitializeComponent();
    }

    public bool ValidateStaticMeshAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is StaticMeshComponentViewModel staticMeshComponentViewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Model)
        {
            var staticMeshComponent = (StaticMeshComponent)staticMeshComponentViewModel.Component;
            var assetContentManager = staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
            staticMeshComponent.AssetId = assetId;
            staticMeshComponent.Mesh = assetContentManager.Load<StaticMesh>(assetId);
            staticMeshComponent.Mesh.Initialize(assetContentManager);
            return true;
        }

        return false;
    }

    public bool ValidateTextureAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is StaticMeshComponentViewModel staticMeshComponentViewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Texture)
        {
            var staticMeshComponent = (StaticMeshComponent)staticMeshComponentViewModel.Component;

            if (staticMeshComponent.Mesh != null)
            {
                var assetContentManager = staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
                staticMeshComponent.Mesh.Texture = assetContentManager.Load<Texture>(assetId);
    
                if (staticMeshComponent.Mesh.Texture?.Resource == null)
                {
                    staticMeshComponent.Mesh.Texture.Load(assetContentManager);
                }
            }
    
            return true;
        }
    
        return false;
    }

    private void StaticMeshComponent_MeshSelection_OnClick(object sender, RoutedEventArgs e)
    {
        var selectStaticMeshWindow = new SelectStaticMeshWindow();
        if (selectStaticMeshWindow.ShowDialog() == true)
        {
            (DataContext as StaticMeshComponentViewModel).CreateMesh(selectStaticMeshWindow.SelectedType);
        }
    }
}