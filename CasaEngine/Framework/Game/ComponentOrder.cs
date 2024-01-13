namespace CasaEngine.Framework.Game;

public enum ComponentUpdateOrder
{
    DebugManager,
    Input,
    Manipulator,
    ScreenManagerComponent,
    ScreenLogComponent,
    Renderer2dComponent,
    Renderer3dComponent,
    Line3dComponent,
    MeshComponent,
    SkinnedMeshComponent,
    ParticleComponent,
    GUI,
    Physics,
    DebugPhysics,
    Default

#if EDITOR
    , CasaEngineEditor
#endif
}

public enum ComponentDrawOrder
{
    GUIBegin,
    DebugManager,
    MeshComponent,
    SkinnedMeshComponent,
    Renderer2dComponent,
    Renderer3dComponent,
    Line3dComponent,
    ParticleComponent,
    DebugPhysics,
    Manipulator,
    GUIEnd,
    ScreenLogComponent
}