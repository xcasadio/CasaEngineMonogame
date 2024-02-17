using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
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
}