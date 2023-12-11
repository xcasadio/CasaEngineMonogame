using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class ComponentListControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(ComponentModelView), typeof(ComponentListControl));

    private GameEditorGui _gameEditor;

    public ComponentModelView SelectedItem
    {
        get => (ComponentModelView)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ComponentListControl()
    {
        InitializeComponent();
    }

    public void InitializeFromGameEditor(GameEditorGui gameEditor)
    {
        _gameEditor = gameEditor;
        _gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        DataContext = new ComponentModelView();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        /*
        var selectedItem = ListBox.SelectedItem as AssetInfoViewModel;
        var spriteData = _gameEditor.Game.GameManager.AssetContentManager.Load<SpriteData>(selectedItem.AssetInfo);
        SelectedItem = new ComponentModelView();*/
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {

        }
    }
}