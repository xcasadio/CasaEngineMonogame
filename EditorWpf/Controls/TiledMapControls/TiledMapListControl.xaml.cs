using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EditorWpf.Controls.TiledMapControls;
using EditorWpf.Controls.Common;

namespace EditorWpf.Controls.TiledMapControls;

public partial class TiledMapListControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(TiledMapDataViewModel), typeof(TiledMapListControl));
    private GameEditorTiledMap _gameEditor;

    public TiledMapDataViewModel? SelectedItem
    {
        get => (TiledMapDataViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public TiledMapListControl()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = ListBox.SelectedItem as TiledMapDataViewModel;
    }

    public void InitializeFromGameEditor(GameEditorTiledMap gameEditor)
    {
        _gameEditor = gameEditor;
        _gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, System.EventArgs e)
    {
        //DataContext = new TiledMapListModelView(_gameEditor);
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {
            //var inputTextBox = new InputTextBox();
            //inputTextBox.Description = "Enter a new name";
            //inputTextBox.Title = "Rename";
            //var TiledMapDataViewModel = (listBox.SelectedItem as TiledMapDataViewModel);
            //inputTextBox.Text = TiledMapDataViewModel.Name;
            //
            //if (inputTextBox.ShowDialog() == true)
            //{
            //    _gameEditor.Game.GameManager.AssetContentManager.Rename(TiledMapDataViewModel.Name, inputTextBox.Text);
            //    TiledMapDataViewModel.Name = inputTextBox.Text;
            //}
        }
    }

    public void LoadTiledMap(string fileName)
    {
        //var TiledMapListModelView = DataContext as TiledMapListModelView;
        //TiledMapListModelView.LoadAnimations2d(fileName);
        //
        //if (TiledMapListModelView.TiledMapDatas.Count > 0)
        //{
        //    Dispatcher.Invoke((Action)(() => ListBox.SelectedIndex = 0));
        //}
    }
}