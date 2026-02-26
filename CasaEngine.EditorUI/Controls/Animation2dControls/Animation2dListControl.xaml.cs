using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;

namespace CasaEngine.EditorUI.Controls.Animation2dControls;

public partial class Animation2dListControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Animation2dDataViewModel), typeof(Animation2dListControl));
    private CasaEngineGame? _game;

    public Animation2dDataViewModel? SelectedItem
    {
        get => (Animation2dDataViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public Animation2dListControl()
    {
        InitializeComponent();
        SortAnimations();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = ListBox.SelectedItem as AssetInfoViewModel;
        var animation2dData = LoadAnimation(selectedItem.Id);
        SelectedItem = new Animation2dDataViewModel(animation2dData);
    }

    private Animation2dData LoadAnimation(Guid id)
    {
        var assetContentManager = _game!.AssetContentManager;
        var graphicsDevice = _game!.GraphicsDevice;
        var animation2dData = assetContentManager.Load<Animation2dData>(id);

        foreach (var frameData in animation2dData.Frames)
        {
            assetContentManager.Load<SpriteData>(frameData.SpriteId);
        }

        return animation2dData;
    }

    /// <summary>Shared-game overload.</summary>
    public void InitializeFromEngineHost(EngineHost host)
    {
        void Wire() { _game = host.Game; DataContext = new Animation2dAssetListModelView(); }
        if (host.IsStarted)
        {
            Wire();
        }
        else
        {
            host.Started += (_, _) => Wire();
        }
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {
            var inputTextBox = new InputTextBox();
            inputTextBox.Description = "Enter a new name";
            inputTextBox.Title = "Rename";
            var animation2dDataViewModel = (listBox.SelectedItem as AssetInfoViewModel);
            inputTextBox.Text = animation2dDataViewModel.Name;

            if (inputTextBox.ShowDialog() == true)
            {
                //_gameEditor.Game.AssetContentManager.Rename(animation2dDataViewModel.Name, inputTextBox.Text);
                animation2dDataViewModel.Name = inputTextBox.Text;
                SortAnimations();
                listBox.ScrollIntoView(listBox.SelectedItem);
            }
        }
    }

    private void SortAnimations()
    {
        ListBox.Items.SortDescriptions.Clear();
        ListBox.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
    }

    public void OpenAnimations2d(string fileName)
    {
        var animation2dListModelView = DataContext as Animation2dAssetListModelView;

        foreach (var assetInfoViewModel in animation2dListModelView.Animation2dAssetInfos)
        {
            if (fileName.EndsWith(assetInfoViewModel.FileName))
            {
                var index = ListBox.Items.IndexOf(assetInfoViewModel);
                Dispatcher.Invoke(() => ListBox.SelectedIndex = index);
                break;
            }
        }
    }

    public void SaveCurrentAnimation()
    {
        if (SelectedItem is Animation2dDataViewModel animation2dDataViewModel)
        {
            AssetSaver.SaveAsset(animation2dDataViewModel.Animation2dData.FileName, animation2dDataViewModel.Animation2dData);
            Logs.WriteInfo($"Animation 2d {animation2dDataViewModel.Animation2dData.Name} saved ({animation2dDataViewModel.Animation2dData.FileName})");
        }
    }
}