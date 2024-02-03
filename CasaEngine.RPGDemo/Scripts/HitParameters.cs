using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Scripts;

public class HitParameters
{
    public int Strength;
    public int Precision;
    public int MagicStrength;

    public AActor Entity;

    public Vector3 ContactPoint;
}