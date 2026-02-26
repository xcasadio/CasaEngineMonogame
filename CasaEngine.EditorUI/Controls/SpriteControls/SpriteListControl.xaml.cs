using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Controls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public partial class SpriteListControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(SpriteDataViewModel), typeof(SpriteListControl));

    private CasaEngineGame?  _game;  // set by InitializeFromEngineHost for shared-game mode

    public SpriteDataViewModel SelectedItem
    {
        get => (SpriteDataViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public SpriteListControl()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = ListBox.SelectedItem as AssetInfoViewModel;
        var spriteData = _game?.AssetContentManager.Load<SpriteData>(selectedItem.Id);
        SelectedItem = new SpriteDataViewModel(spriteData);
    }

    /// <summary>Shared-game overload: subscribe to <paramref name="host"/>.<see cref="EngineHost.Started"/>.</summary>
    public void InitializeFromEngineHost(EngineHost host)
    {
        void Wire()
        {
            _game = host.Game;
            DataContext = new SpritesModelView();
        }
        if (host.IsStarted) Wire();
        else host.Started += (_, _) => Wire();
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {
            var inputTextBox = new InputTextBox();
            inputTextBox.Description = "Enter a new name";
            inputTextBox.Title = "Rename";
            var spriteDataViewModel = (listBox.SelectedItem as AssetInfoViewModel);
            inputTextBox.Text = spriteDataViewModel.Name;

            if (inputTextBox.ShowDialog() == true)
            {
                //_gameEditor.Game.AssetContentManager.Rename(spriteDataViewModel.Name, inputTextBox.Text);
                spriteDataViewModel.Name = inputTextBox.Text;
            }
        }
    }

    public void OpenSprite(string fileName)
    {
        var spritesModelView = DataContext as SpritesModelView;

        foreach (var assetInfoViewModel in spritesModelView.SpriteAssetInfos)
        {
            if (fileName.EndsWith(assetInfoViewModel.FileName))
            {
                var index = ListBox.Items.IndexOf(assetInfoViewModel);
                Dispatcher.Invoke(() => ListBox.SelectedIndex = index);
                break;
            }
        }
    }

    public void SaveCurrentSprite()
    {
        if (SelectedItem is SpriteDataViewModel spriteDataViewModel)
        {
            AssetSaver.SaveAsset(spriteDataViewModel.SpriteData.FileName, spriteDataViewModel.SpriteData);
            Logs.WriteInfo($"Sprite {spriteDataViewModel.SpriteData.Name} saved ({spriteDataViewModel.SpriteData.FileName})");
        }
    }
}