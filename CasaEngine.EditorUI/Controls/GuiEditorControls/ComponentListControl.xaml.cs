using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

public partial class ComponentListControl : UserControl
{
    private GameEditorGui _gameEditor;
    private bool _keyDeletePressed;
    private bool selectionLock;

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
        //DataContext = new ScreenViewModel();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectionLock == false)
        {
            selectionLock = true;
            (DataContext as ScreenViewModel).SelectedControl = ListBox.SelectedItem as ControlViewModel;
            selectionLock = false;
        }
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem != null)
        {

        }
    }

    private void ListBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_keyDeletePressed && e.Key == Key.Delete)
        {
            var screenViewModel = DataContext as ScreenViewModel;
            screenViewModel.RemoveSelectedControl();

            _keyDeletePressed = true;
        }
    }

    private void ListBox_OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            _keyDeletePressed = false;
        }
    }
}