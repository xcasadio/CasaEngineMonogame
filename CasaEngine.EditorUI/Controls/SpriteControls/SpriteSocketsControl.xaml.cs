using System.Windows;
using System.Windows.Controls;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public partial class SpriteSocketsControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(SocketViewModel), typeof(SpriteSocketsControl));

    public SocketViewModel? SelectedItem
    {
        get => (SocketViewModel?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public SpriteSocketsControl()
    {
        InitializeComponent();
    }

    private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = ListBox.SelectedItem as SocketViewModel;
    }
}