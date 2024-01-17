using System.Windows;
using System.Windows.Data;
using CasaEngine.WpfControls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace CasaEngine.EditorUI.Controls.PropertyGridTypeEditor;

public class RectangleTypeEditorControl : ITypeEditor
{
    public FrameworkElement ResolveEditor(PropertyItem propertyItem)
    {
        var rectangleEditor = new RectangleEditor { DataContext = propertyItem.Instance };
        var binding = new Binding("DataContext." + propertyItem.PropertyName) { Source = rectangleEditor };
        rectangleEditor.SetBinding(RectangleEditor.ValueProperty, binding);
        rectangleEditor.Value = (Rectangle)propertyItem.Value;
        return rectangleEditor;
    }
}