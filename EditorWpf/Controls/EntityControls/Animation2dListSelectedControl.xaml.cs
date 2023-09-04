using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.EntityControls
{
    public partial class Animation2dListSelectedControl : UserControl
    {
        public Animation2dListSelectedControl()
        {
            DataContextChanged += OnDataContextChanged;

            InitializeComponent();
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var animatedSpriteComponent = DataContext as AnimatedSpriteComponent;

            var animation2dSelectedListModelView = new Animation2dSelectedListModelView(animatedSpriteComponent);

            animation2dList.ItemsSource = animation2dSelectedListModelView.Animations;
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
