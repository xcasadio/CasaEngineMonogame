using System.Windows;
using System.Windows.Controls;

namespace CasaEngine.Editor.Controls.SpriteControls;

public partial class SpriteCollisionsControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(Collision2dViewModel), typeof(SpriteCollisionsControl));

    public Collision2dViewModel? SelectedItem
    {
        get => (Collision2dViewModel?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public SpriteCollisionsControl()
    {
        InitializeComponent();
    }

    private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = ListBox.SelectedItem as Collision2dViewModel;
    }
}