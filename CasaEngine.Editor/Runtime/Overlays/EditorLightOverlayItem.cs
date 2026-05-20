using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Runtime.Overlays;

public readonly record struct EditorLightOverlayItem(
    Entity Owner,
    LightComponent Light,
    LightType Type,
    Vector3 Position,
    Vector3 Direction,
    float Range,
    float InnerConeAngleRadians,
    float OuterConeAngleRadians,
    Color Color,
    bool IsSelected);