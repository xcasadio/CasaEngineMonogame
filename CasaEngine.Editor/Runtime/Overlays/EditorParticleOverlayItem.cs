using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime.Overlays;

public readonly record struct EditorParticleOverlayItem(
    Entity Entity,
    ParticleSystemComponent Component,
    ParticleEffectAsset Asset,
    Matrix WorldMatrix,
    BoundingBox Bounds,
    bool HasBounds,
    bool IsSelected);