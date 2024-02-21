using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.Common;

public partial class AssetSelectorControl : UserControl
{
    private const string NoAssetDefined = "no asset defined";

    public static readonly DependencyProperty AssetIdItemProperty = DependencyProperty.Register(nameof(AssetId), typeof(Guid), typeof(AssetSelectorControl), new FrameworkPropertyMetadata(Guid.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, null));
    public static readonly DependencyProperty AssetFullPathProperty = DependencyProperty.Register(nameof(AssetFullPath), typeof(string), typeof(AssetSelectorControl));

    public delegate bool ValidateAssetDelegate(object owner, Guid assetId, string assetFullName);

    public ValidateAssetDelegate? ValidateAsset { get; set; }

    public Guid AssetId
    {
        get => (Guid)GetValue(AssetIdItemProperty);
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
        var selectedItem = contentBrowserControl.SelectedItem;

        if (selectedItem != null && sender is FrameworkElement frameworkElement)
        {
            if (ValidateAsset?.Invoke(frameworkElement.DataContext, selectedItem.Id, selectedItem.FileName) ?? false)
            {
                SetAssetInfoDescription(selectedItem);
            }
        }
    }

    private void SetAssetInfoDescription(AssetInfo assetInfo)
    {
        AssetFullPath = assetInfo.FileName;
        AssetId = assetInfo.Id;
    }

    private static void OnComponentPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var assetSelectorControl = (AssetSelectorControl)sender;
        assetSelectorControl.OnComponentPropertyChanged(e);
    }

    private void OnComponentPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        var assetInfo = AssetCatalog.Get(AssetId);
        SetCurrentValue(AssetFullPathProperty, assetInfo != null ? assetInfo.FileName : NoAssetDefined);
    }
}