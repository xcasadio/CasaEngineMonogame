using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Engine.Physics.Shapes;

namespace EditorWpf.Controls.WorldControls;

public class ShapeTemplateSelector : DataTemplateSelector
{
    public DataTemplate BoxTemplate { get; set; }
    public DataTemplate SphereTemplate { get; set; }
    public DataTemplate CylinderTemplate { get; set; }
    public DataTemplate CapsuleTemplate { get; set; }
    public DataTemplate CompoundTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is Shape shape)
        {
            return shape.Type switch
            {
                ShapeType.Box => BoxTemplate,
                ShapeType.Sphere => SphereTemplate,
                ShapeType.Cylinder => CylinderTemplate,
                ShapeType.Capsule => CapsuleTemplate,
                ShapeType.Compound => CompoundTemplate,
                _ => throw new NotSupportedException($"{shape.Type} is not supported")
            };
        }

        return null;
    }
}