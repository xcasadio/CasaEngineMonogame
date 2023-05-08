using System.Windows;
using System.Windows.Data;
using CasaEngine.WpfControls;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace EditorWpf.Controls.PropertyGridTypeEditor;

public class PointTypeEditorControl : ITypeEditor
{
    public FrameworkElement ResolveEditor(PropertyItem propertyItem)
    {
        var pointEditor = new PointEditor { DataContext = propertyItem.Instance };
        var binding = new Binding("DataContext." + propertyItem.PropertyName) { Source = pointEditor };
        pointEditor.SetBinding(PointEditor.ValueProperty, binding);
        pointEditor.Value = (Microsoft.Xna.Framework.Point?)propertyItem.Value;
        return pointEditor;
    }
}