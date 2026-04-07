using Microsoft.Xna.Framework;
using System;

namespace CasaEngine.Framework.AI.Navigation;

public readonly record struct SteeringForceVector(double X, double Y, double Z)
{
    public static SteeringForceVector Zero => new(0.0, 0.0, 0.0);

    public SteeringForceVector Multiply(double scalar)
    {
        return new SteeringForceVector(X * scalar, Y * scalar, Z * scalar);
    }
    
    public double LengthSquared()
    {
        return X * X + Y * Y + Z * Z;
    }
    
    public double Length()
    {
        return Math.Sqrt(LengthSquared());
    }
    
    public SteeringForceVector Truncate(double maxLength)
    {
        double lengthSquared = LengthSquared();
        double maxLengthSquared = maxLength * maxLength;
        if (lengthSquared <= maxLengthSquared || lengthSquared <= double.Epsilon)
        {
            return this;
        }
        
        double scale = maxLength / Math.Sqrt(lengthSquared);
        return Multiply(scale);
    }

    public Vector3 ToVector3()
    {
        return new Vector3((float)X, (float)Y, (float)Z);
    }
}