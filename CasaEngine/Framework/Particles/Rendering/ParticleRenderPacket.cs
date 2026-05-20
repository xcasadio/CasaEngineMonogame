using CasaEngine.Framework.Particles;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Rendering;

public struct ParticleRenderPacket
{
    public Vector3 Position;
    public Vector2 Size;
    public float Rotation;
    public Color Color;
    public float Alpha;
    public Guid TextureAssetId;
    public ParticleBlendMode BlendMode;
    public ParticleSortMode SortMode;
    public ParticleRenderMode RenderMode;
    public bool DepthTest;
    public bool DepthWrite;
    public int RenderQueue;
    public int Layer;
    public int EmitterIndex;
    public int ParticleIndex;
    public float DistanceToCameraSquared;
}