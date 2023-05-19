using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers;

public static class QuaternionExtension
{
    public static Vector3 GetYawPitchRoll(this Quaternion quaternion)
    {
        var rotationaxes = new Vector3();

        var forward = Vector3.Transform(Vector3.Forward, quaternion);
        var up = Vector3.Transform(Vector3.Up, quaternion);
        rotationaxes = Vector3Helper.AngleTo(new Vector3(), forward);
        if (rotationaxes.X == MathHelper.PiOver2)
        {
            rotationaxes.Y = TrigonometryHelper.ArcTanAngle(up.Z, up.X);
            rotationaxes.Z = 0;
        }
        else if (rotationaxes.X == -MathHelper.PiOver2)
        {
            rotationaxes.Y = TrigonometryHelper.ArcTanAngle(-up.Z, -up.X);
            rotationaxes.Z = 0;
        }
        else
        {
            up = Vector3.Transform(up, Matrix.CreateRotationY(-rotationaxes.Y));
            up = Vector3.Transform(up, Matrix.CreateRotationX(-rotationaxes.X));
            rotationaxes.Z = TrigonometryHelper.ArcTanAngle(up.Y, -up.X);
        }

        // Special cases.
        if (rotationaxes.Y <= (float)-Math.PI)
        {
            rotationaxes.Y = (float)Math.PI;
        }

        if (rotationaxes.Z <= (float)-Math.PI)
        {
            rotationaxes.Z = (float)Math.PI;
        }

        if (rotationaxes.Y >= Math.PI && rotationaxes.Z >= Math.PI)
        {
            rotationaxes.Y = 0;
            rotationaxes.Z = 0;
            rotationaxes.X = (float)Math.PI - rotationaxes.X;
        }

        return new Vector3(rotationaxes.Y, rotationaxes.X, rotationaxes.Z);
    }
}