using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.EntityControls
{
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
            var animatedSpriteComponent = DataContext as AnimatedSpriteComponent;

            _animation2dSelectedListModelView = new Animation2dSelectedListModelView(animatedSpriteComponent);

            animation2dList.ItemsSource = _animation2dSelectedListModelView.Animations;
        }

        private void ButtonAddAnimation_OnClick(object sender, RoutedEventArgs e)
        {
            var animation2dListSelectorWindow = new Animation2dListSelectorWindow(_animation2dSelectedListModelView.Animations);
            animation2dListSelectorWindow.Owner = Application.Current.MainWindow;

            if (animation2dListSelectorWindow.ShowDialog() == true)
            {
                foreach (var assetInfoViewModel in animation2dListSelectorWindow.AssetInfoSelected)
                {
                    _animation2dSelectedListModelView.Add(assetInfoViewModel.AssetInfo);
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

    internal class Animation2dSelectedListModelView
    {
        private AnimatedSpriteComponent _animatedSpriteComponent;

        public ObservableCollection<AssetInfoViewModel> Animations { get; } = new();

        public Animation2dSelectedListModelView(AnimatedSpriteComponent animatedSpriteComponent)
        {
            _animatedSpriteComponent = animatedSpriteComponent;

            foreach (var animationAssetId in _animatedSpriteComponent.AnimationAssetIds)
            {
                var assetInfo = GameSettings.AssetInfoManager.Get(animationAssetId);
                Add(assetInfo);
            }
        }

        public void Add(AssetInfo assetInfo)
        {
            Animations.Add(new AssetInfoViewModel(assetInfo));
        }

        public void Remove(AssetInfoViewModel assetInfoViewModel)
        {
            Animations.Remove(assetInfoViewModel);
        }
    }
}
