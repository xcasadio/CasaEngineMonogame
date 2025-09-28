using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Graphics;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class StaticMeshComponentControl : UserControl
{
    public StaticMeshComponentControl()
    {
        InitializeComponent();
    }

    public bool ValidateStaticMeshAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is StaticMeshComponent StaticMeshComponent
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Model)
        {
            var assetContentManager = StaticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
            StaticMeshComponent.AssetId = assetId;
            StaticMeshComponent.Mesh = assetContentManager.Load<StaticMesh>(assetId);
            StaticMeshComponent.Mesh.Initialize(assetContentManager);
            return true;
        }

        return false;
    }

    //public bool ValidateStaticMeshAsset(object owner, Guid assetId, string assetFullName)
    //{
    //    if (owner is StaticMeshComponent staticMeshComponent
    //        && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Texture)
    //    {
    //        if (staticMeshComponent.Mesh != null)
    //        {
    //            var assetContentManager = staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
    //            staticMeshComponent.Mesh.Texture = assetContentManager.Load<Texture>(assetId);
    //
    //            if (staticMeshComponent.Mesh.Texture?.Resource == null)
    //            {
    //                staticMeshComponent.Mesh.Texture.Load(assetContentManager);
    //            }
    //        }
    //
    //        return true;
    //    }
    //
    //    return false;
    //}

    private void StaticMeshComponent_MeshSelection_OnClick(object sender, RoutedEventArgs e)
    {
        var selectStaticMeshWindow = new SelectStaticMeshWindow();
        if (selectStaticMeshWindow.ShowDialog() == true)
        {
            (DataContext as StaticMeshComponentViewModel).CreateMesh(selectStaticMeshWindow.SelectedType);
        }
    }
}