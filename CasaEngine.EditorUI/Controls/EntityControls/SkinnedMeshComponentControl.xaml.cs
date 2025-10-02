using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class SkinnedMeshComponentControl : UserControl
{
    public SkinnedMeshComponentControl()
    {
        InitializeComponent();
    }

    public bool ValidateSkinnedMeshAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is SkinnedMeshComponentViewModel skinnedMeshComponentViewModel
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Model)
        {
            var skinnedMeshComponent = skinnedMeshComponentViewModel.Component as SkinnedMeshComponent;
            var assetContentManager = skinnedMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
            skinnedMeshComponent.SkinnedMeshAssetId = assetId;
            skinnedMeshComponent.SkinnedMesh = assetContentManager.Load<SkinnedMesh>(assetId);
            skinnedMeshComponent.SkinnedMesh.Initialize(assetContentManager);
            return true;
        }

        return false;
    }
}