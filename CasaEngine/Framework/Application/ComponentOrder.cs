namespace CasaEngine.Framework.Application;

public enum ComponentUpdateOrder
{
    DebugManager,
    Input,
    GUI,
    Manipulator,
    ScreenManagerComponent,
    ScreenLogComponent,
    Renderer2dComponent,
    Renderer3dComponent,
    Line3dComponent,
    MeshComponent,
    SkinnedMeshComponent,
    ParticleComponent,
    Physics,
    DebugPhysics,
    Default,
    CasaEngineEditor
}

public enum ComponentDrawOrder
{
    DebugManager,
    GUIBegin,
    MeshComponent,
    SkinnedMeshComponent,
    Renderer2dComponent,
    Renderer3dComponent,
    Line3dComponent,
    ParticleComponent,
    DebugPhysics,
    Manipulator,
    GUI,
    ScreenLogComponent
}