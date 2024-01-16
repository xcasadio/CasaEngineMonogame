using CasaEngine.Core.Shapes;
using CasaEngine.WpfControls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CasaEngine.Framework.Assets.Sprites;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace CasaEngine.Editor.Controls.SpriteControls;

public class Collision2dViewModel : NotifyPropertyChangeBase
{
    private readonly Collision2d _collision2d;

    public CollisionHitType CollisionHitType
    {
        get => _collision2d.CollisionHitType;
        set
        {
            if (value.Equals(_collision2d.CollisionHitType)) return;
            _collision2d.CollisionHitType = value;
            OnPropertyChanged();
        }
    }

    [Editor(typeof(Shape2dTypeEditorControl), typeof(Shape2dTypeEditorControl))]
    public Shape2d Shape
    {
        get => _collision2d.Shape;
        set
        {
            if (value.Equals(_collision2d.Shape)) return;
            _collision2d.Shape = value;
            OnPropertyChanged();
        }
    }

    public Collision2dViewModel(Collision2d collision2d)
    {
        _collision2d = collision2d;
    }

    public override string ToString()
    {
        return _collision2d.ToString();
    }
}

public class Shape2dTypeEditorControl : ITypeEditor
{
    public FrameworkElement ResolveEditor(PropertyItem propertyItem)
    {
        var collision2dViewModel = propertyItem.Instance as Collision2dViewModel;
        var binding = new Binding("DataContext." + propertyItem.PropertyName);

        switch (collision2dViewModel.Shape)
        {
            case ShapeRectangle:
                var rectangleEditor = new ShapeRectangleEditor { DataContext = collision2dViewModel };
                binding.Source = rectangleEditor;
                rectangleEditor.SetBinding(ShapeRectangleEditor.ValueProperty, binding);
                rectangleEditor.Value = (ShapeRectangle)propertyItem.Value;
                return rectangleEditor;

            case ShapeCircle:
                var circleEditor = new ShapeCircleEditor { DataContext = collision2dViewModel };
                binding.Source = circleEditor;
                circleEditor.SetBinding(ShapeCircleEditor.ValueProperty, binding);
                circleEditor.Value = (ShapeCircle)propertyItem.Value;
                return circleEditor;

            case ShapePolygone:
                break;
        }

        return null;
    }
}