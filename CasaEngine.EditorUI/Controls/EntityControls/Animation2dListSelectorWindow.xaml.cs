using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class Animation2dListSelectorWindow : Window
{
    public IEnumerable<AssetInfoViewModel> AssetInfoSelected
    {
        get
        {
            foreach (var item in listBoxAnimations.SelectedItems)
            {
                yield return (AssetInfoViewModel)item;
            }
        }
    }

    public Animation2dListSelectorWindow(IEnumerable<AssetInfoViewModel> animationsToFilter = null)
    {
        InitializeComponent();

        var assetInfos = AssetCatalog.AssetInfos
            .Where(x => string.Equals(Path.GetExtension(x.FileName), Constants.FileNameExtensions.Animation2d))
            .Select(x => new AssetInfoViewModel(x));

        if (animationsToFilter != null)
        {
            assetInfos = assetInfos.Except(animationsToFilter);
        }

        listBoxAnimations.ItemsSource = assetInfos.ToList();
    }

    private void ButtonOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}