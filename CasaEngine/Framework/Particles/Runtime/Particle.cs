using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Runtime;

public struct Particle
{
    public bool IsAlive;
    public float Age;
    public float Lifetime;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector2 Size;
    public float Rotation;
    public float AngularVelocity;
    public Color StartColor;
    public Color Color;
    public float Alpha;
}