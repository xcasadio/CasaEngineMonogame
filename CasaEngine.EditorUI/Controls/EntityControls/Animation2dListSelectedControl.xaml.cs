using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class Animation2dListSelectedControl : UserControl
{
    private Animation2dSelectedListModelView _animation2dSelectedListModelView;

    public Animation2dListSelectedControl()
    {
        DataContextChanged += OnDataContextChanged;

        InitializeComponent();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext != null)
        {
            var animatedSpriteComponent = DataContext as AnimatedSpriteComponent;
            _animation2dSelectedListModelView = new Animation2dSelectedListModelView(animatedSpriteComponent);
            animation2dList.ItemsSource = _animation2dSelectedListModelView.Animations;
        }
    }

    private void ButtonAddAnimation_OnClick(object sender, RoutedEventArgs e)
    {
        var animation2dListSelectorWindow = new Animation2dListSelectorWindow(_animation2dSelectedListModelView.Animations);
        animation2dListSelectorWindow.Owner = Application.Current.MainWindow;

        if (animation2dListSelectorWindow.ShowDialog() == true)
        {
            var animatedSpriteComponent = DataContext as AnimatedSpriteComponent;
            var assetContentManager = animatedSpriteComponent.Owner.World.Game.AssetContentManager;

            foreach (var assetInfoViewModel in animation2dListSelectorWindow.AssetInfoSelected)
            {
                _animation2dSelectedListModelView.Add(assetInfoViewModel.Id);
            }
        }
    }

    private void ButtonDeleteAnimation_OnClick(object sender, RoutedEventArgs e)
    {
        var toRemove = new List<AssetInfoViewModel>();
        foreach (AssetInfoViewModel assetInfoViewModel in animation2dList.SelectedItems)
        {
            toRemove.Add(assetInfoViewModel);
        }

        foreach (var assetInfoViewModel in toRemove)
        {
            _animation2dSelectedListModelView.Remove(assetInfoViewModel);
        }
    }
}