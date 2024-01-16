using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Editor.Controls.EntityControls;

public class ShapeTemplateSelector : DataTemplateSelector
{
    public DataTemplate BoxTemplate { get; set; }
    public DataTemplate SphereTemplate { get; set; }
    public DataTemplate CylinderTemplate { get; set; }
    public DataTemplate CapsuleTemplate { get; set; }
    public DataTemplate CompoundTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is Shape3d shape)
        {
            return shape.Type switch
            {
                Shape3dType.Box => BoxTemplate,
                Shape3dType.Sphere => SphereTemplate,
                Shape3dType.Cylinder => CylinderTemplate,
                Shape3dType.Capsule => CapsuleTemplate,
                Shape3dType.Compound => CompoundTemplate,
                _ => throw new NotSupportedException($"{shape.Type} is not supported")
            };
        }

        return null;
    }
}