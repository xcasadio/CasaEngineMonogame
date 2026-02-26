using System;

namespace GizmoTools;

public class TransformationEventArgs
{
    public readonly ValueType Value;

    public TransformationEventArgs(ValueType value)
    {
        Value = value;
    }
}

public delegate void TransformationEventHandler(ITransformable transformable, TransformationEventArgs e);
