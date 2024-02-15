using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Framework.GUI;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

public partial class PlaceControlsControl : UserControl
{
    public PlaceControlsControl()
    {
        InitializeComponent();
        LoadControls();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var frameworkElement = sender as FrameworkElement;

            DragDrop.DoDragDrop(frameworkElement,
                JsonSerializer.Serialize(new DragAndDropInfo
                {
                    Action = DragAndDropInfoAction.Create,
                    Type = frameworkElement.Tag.ToString()
                }), DragDropEffects.Copy);
        }
    }

    private void LoadControls()
    {
        foreach (var type in ControlHelper.TypesByName.Values.OrderBy(t => t.Name))
        {
            var label = new Label
            {
                Content = type.Name,
                Cursor = Cursors.Hand,
                Tag = type.Name
            };
            label.MouseMove += OnMouseMove;

            stackPanel.Children.Add(label);
        }
    }
}