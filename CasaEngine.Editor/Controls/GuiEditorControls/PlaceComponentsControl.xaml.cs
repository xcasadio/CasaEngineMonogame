using System.Reflection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Editor.DragAndDrop;
using TomShane.Neoforce.Controls;
using TomShane.Neoforce.Controls.Skins;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class PlaceComponentsControl : UserControl
{
    public PlaceComponentsControl()
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
        var dllFileName = Path.Combine(Environment.CurrentDirectory, "TomShane.Neoforce.Controls.dll");
        var assembly = Assembly.LoadFrom(dllFileName);

        foreach (var type in assembly.GetTypes()
                     .Where(t =>
                         !string.Equals(nameof(Skin), t.Name, StringComparison.InvariantCultureIgnoreCase)
                         && !string.Equals(nameof(Renderer), t.Name, StringComparison.InvariantCultureIgnoreCase)
                         && t is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                         && t.IsSubclassOf(typeof(Component)))
                     .OrderBy(t => t.Name))
        {
            var label = new System.Windows.Controls.Label
            {
                Content = type.Name,
                Cursor = Cursors.Hand,
                Tag = type.FullName
            };
            label.MouseMove += OnMouseMove;

            stackPanel.Children.Add(label);
        }
    }
}