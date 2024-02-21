using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Engine;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class SkinnedMeshComponentControl : UserControl
{
    public static readonly DependencyProperty SkinnedMeshComponentViewModelProperty = DependencyProperty.Register(nameof(SkinnedMeshComponentViewModel), typeof(SkinnedMeshComponentViewModel), typeof(SkinnedMeshComponentControl));

    public SkinnedMeshComponentViewModel? SkinnedMeshComponentViewModel
    {
        get => (SkinnedMeshComponentViewModel)GetValue(SkinnedMeshComponentViewModelProperty);
        set => SetValue(SkinnedMeshComponentViewModelProperty, value);
    }

    public SkinnedMeshComponentControl()
    {
        DataContextChanged += OnDataContextChanged;
        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is SkinnedMeshComponent skinnedMeshComponent)
        {
            SkinnedMeshComponentViewModel = new SkinnedMeshComponentViewModel(skinnedMeshComponent);
        }
    }

    public bool ValidateSkinnedMeshAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is SkinnedMeshComponent skinnedMeshComponent
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Model)
        {
            var assetContentManager = skinnedMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
            skinnedMeshComponent.SkinnedMeshAssetId = assetId;
            skinnedMeshComponent.SkinnedMesh = assetContentManager.Load<SkinnedMesh>(assetId);
            skinnedMeshComponent.SkinnedMesh.Initialize(assetContentManager);
            return true;
        }

        return false;
    }
}