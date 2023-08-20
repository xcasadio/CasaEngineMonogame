using Microsoft.Xna.Framework;
using System;

namespace CasaEngine.Core.Helpers;

public static class RandomHelper
{
    public static Random Create()
    {
        return new Random(Environment.TickCount);
    }

    public static float Variation(this Random random, float value, float variation)
    {
        float min = value - variation;
        float max = value + variation;

        return random.NextFloat(min, max);
    }

    public static int Choose(this Random random, params int[] values)
    {
        int index = random.NextInt32(0, values.Length);
        return values[index];
    }

    public static float Choose(this Random random, params float[] values)
    {
        int index = random.NextInt32(0, values.Length);
        return values[index];
    }

    public static T Choose<T>(this Random random, params T[] values)
    {
        int index = random.NextInt32(0, values.Length);
        return values[index];
    }

    public static float NextFloat(this Random random, float min, float max) => MathHelper.Lerp(min, max, (float)random.NextDouble());


    public static int NextInt32(this Random random, int min, int max) => Math.Abs(random.Next() % (max - min + 1)) + min;

    public static long NextLong(this Random random)
    {
        byte[] buffer = new byte[8];
        random.NextBytes(buffer);
        return BitConverter.ToInt64(buffer, 0);
    }

    public static long NextLong(this Random random, long min, long max)
    {
        byte[] buffer = new byte[8];
        random.NextBytes(buffer);
        return Math.Abs(BitConverter.ToInt64(buffer, 0) % (max - min + 1L)) + min;
    }

    public static Vector2 NextVector2(this Random random, Vector2 min, Vector2 max) => new Vector2(random.NextFloat(min.X, max.X), random.NextFloat(min.Y, max.Y));

    public static Vector3 NextVector3(this Random random, Vector3 min, Vector3 max) => new Vector3(random.NextFloat(min.X, max.X), random.NextFloat(min.Y, max.Y), random.NextFloat(min.Z, max.Z));

    public static Vector4 NextVector4(this Random random, Vector4 min, Vector4 max) => new Vector4(random.NextFloat(min.X, max.X), random.NextFloat(min.Y, max.Y), random.NextFloat(min.Z, max.Z), random.NextFloat(min.W, max.W));

    public static Color NextColor(this Random random) => new Color(random.NextFloat(0.0f, 1f), random.NextFloat(0.0f, 1f), random.NextFloat(0.0f, 1f), 1f);

    public static Color NextColor(this Random random, float minBrightness, float maxBrightness) => new Color(random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), 1f);

    public static Color NextColor(
      this Random random,
      float minBrightness,
      float maxBrightness,
      float alpha)
    {
        return new Color(random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), alpha);
    }

    public static Color NextColor(
      this Random random,
      float minBrightness,
      float maxBrightness,
      float minAlpha,
      float maxAlpha)
    {
        return new Color(random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minBrightness, maxBrightness), random.NextFloat(minAlpha, maxAlpha));
    }

    public static Point NextPoint(this Random random, Point min, Point max) => new Point(random.Next(min.X, max.X), random.Next(min.Y, max.Y));

    public static TimeSpan NextTime(this Random random, TimeSpan min, TimeSpan max) => TimeSpan.FromTicks(random.NextLong(min.Ticks, max.Ticks));
}