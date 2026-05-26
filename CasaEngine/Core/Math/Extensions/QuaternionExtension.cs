using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Math.Extensions;

public static class QuaternionExtension
{
    public static Vector3 GetYawPitchRoll(this Quaternion quaternion)
    {
        var rotationaxes = new Vector3();

        var forward = Vector3.Transform(Vector3.Forward, quaternion);
        var up = Vector3.Transform(Vector3.Up, quaternion);
        rotationaxes = Geometry3D.AngleTo(new Vector3(), forward);
        if (MathUtils.NearEqual(rotationaxes.X, MathHelper.PiOver2))
        {
            rotationaxes.Y = MathF.Atan2(up.Z, up.X);
            rotationaxes.Z = 0;
        }
        else if (MathUtils.NearEqual(rotationaxes.X, -MathHelper.PiOver2))
        {
            rotationaxes.Y = MathF.Atan2(-up.Z, -up.X);
            rotationaxes.Z = 0;
        }
        else
        {
            up = Vector3.Transform(up, Matrix.CreateRotationY(-rotationaxes.Y));
            up = Vector3.Transform(up, Matrix.CreateRotationX(-rotationaxes.X));
            rotationaxes.Z = MathF.Atan2(up.Y, -up.X);
        }

        // Special cases.
        if (rotationaxes.Y <= -MathHelper.Pi)
        {
            rotationaxes.Y = MathHelper.Pi;
        }

        if (rotationaxes.Z <= -MathHelper.Pi)
        {
            rotationaxes.Z = MathHelper.Pi;
        }

        if (rotationaxes.Y >= System.Math.PI && rotationaxes.Z >= System.Math.PI)
        {
            rotationaxes.Y = 0;
            rotationaxes.Z = 0;
            rotationaxes.X = MathHelper.Pi - rotationaxes.X;
        }

        return new Vector3(rotationaxes.Y, rotationaxes.X, rotationaxes.Z);
    }
}