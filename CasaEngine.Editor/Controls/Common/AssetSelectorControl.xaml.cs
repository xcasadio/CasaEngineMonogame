using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.Common
{
    public partial class AssetSelectorControl : UserControl
    {
        private const string NoAssetDefined = "no asset defined";

        public static readonly DependencyProperty AssetIdItemProperty = DependencyProperty.Register(nameof(AssetId), typeof(long), typeof(AssetSelectorControl), new FrameworkPropertyMetadata(IdManager.InvalidId, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, null));
        public static readonly DependencyProperty AssetFullPathProperty = DependencyProperty.Register(nameof(AssetFullPath), typeof(string), typeof(AssetSelectorControl));

        public long AssetId
        {
            get => (long)GetValue(AssetIdItemProperty);
            set => SetValue(AssetIdItemProperty, value);
        }

        public string AssetFullPath
        {
            get => (string)GetValue(AssetFullPathProperty);
            set => SetValue(AssetFullPathProperty, value);
        }

        public AssetSelectorControl()
        {
            InitializeComponent();

            AssetFullPath = NoAssetDefined;
        }

        private void SetAssetInfo_OnClick(object sender, RoutedEventArgs e)
        {
            var contentBrowserControl = this.FindParent<MainWindow>().ContentBrowserControl;

            if (contentBrowserControl.SelectedItem != null && sender is FrameworkElement frameworkElement)
            {
                if (frameworkElement.DataContext is TileMapComponent tileMapComponent
                    && System.IO.Path.GetExtension(contentBrowserControl.SelectedItem.FileName) ==
                    Constants.FileNameExtensions.TileMap)
                {
                    tileMapComponent.TileMapDataAssetId = contentBrowserControl.SelectedItem.Id;

                    SetAssetInfoDescription(contentBrowserControl.SelectedItem);
                }
                else if (frameworkElement.DataContext is StaticMeshComponent staticMeshComponent
                         && System.IO.Path.GetExtension(contentBrowserControl.SelectedItem.FileName) ==
                         Constants.FileNameExtensions.Texture)
                {
                    staticMeshComponent.Mesh.Texture =
                        staticMeshComponent.Owner.Game.GameManager.AssetContentManager.Load<Texture>(
                            contentBrowserControl.SelectedItem);

                    SetAssetInfoDescription(contentBrowserControl.SelectedItem);
                }
            }
        }

        private void SetAssetInfoDescription(AssetInfo assetInfo)
        {
            AssetFullPath = assetInfo.FileName;
        }

        protected static void OnComponentPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var assetSelectorControl = (AssetSelectorControl)sender;
            assetSelectorControl.OnComponentPropertyChanged(e);
        }

        private void OnComponentPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(AssetId);
            SetCurrentValue(AssetFullPathProperty, assetInfo != null ? assetInfo.FileName : NoAssetDefined);

            //var isInitializing = !_templateApplied && _initializingProperty == null;
            //if (isInitializing)
            //{
            //    _initializingProperty = e.Property;
            //}
            //
            //if (!_interlock)
            //{
            //    _interlock = true;
            //    Value = UpdateValueFromComponent(e.Property);
            //    SetCurrentValue(XProperty, GetDisplayValue(_decomposedRotation.X));
            //    _interlock = false;
            //}
            //
            //UpdateBinding(e.Property);
            //if (isInitializing)
            //{
            //    _initializingProperty = null;
            //}
        }
    }
}
