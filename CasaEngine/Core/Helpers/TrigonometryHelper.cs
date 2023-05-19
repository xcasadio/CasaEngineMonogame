using MathXna = Microsoft.Xna.Framework.MathHelper;

namespace CasaEngine.Core.Helpers;

public static class TrigonometryHelper
{
    public static float RevolutionsToDegrees(float revolution)
    {
        return revolution * 360.0f;
    }

    public static float RevolutionsToRadians(float revolution)
    {
        return revolution * MathXna.TwoPi;
    }

    public static float RevolutionsToGradians(float revolution)
    {
        return revolution * 400.0f;
    }

    public static float DegreesToRevolutions(float degree)
    {
        return degree / 360.0f;
    }

    public static float DegreesToRadians(float degree)
    {
        return degree * (MathXna.Pi / 180.0f);
    }

    public static float RadiansToRevolutions(float radian)
    {
        return radian / MathXna.TwoPi;
    }

    public static float RadiansToGradians(float radian)
    {
        return radian * (200.0f / MathXna.Pi);
    }

    public static float GradiansToRevolutions(float gradian)
    {
        return gradian / 400.0f;
    }

    public static float GradiansToDegrees(float gradian)
    {
        return gradian * (9.0f / 10.0f);
    }

    public static float GradiansToRadians(float gradian)
    {
        return gradian * (MathXna.Pi / 200.0f);
    }

    public static float RadiansToDegrees(float radian)
    {
        return radian * (180.0f / MathXna.Pi);
    }

    public static float ArcTanAngle(float x, float y)
    {
        if (x == 0)
        {
            if (y == 1)
            {
                return MathXna.PiOver2;
            }

            return -MathXna.PiOver2;
        }
        if (x > 0)
        {
            return (float)Math.Atan(y / x);
        }

        if (x < 0)
        {
            if (y > 0)
            {
                return (float)Math.Atan(y / x) + MathXna.Pi;
            }

            return (float)Math.Atan(y / x) - MathXna.Pi;
        }
        return 0;
    }
}