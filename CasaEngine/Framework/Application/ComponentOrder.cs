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
    CasaEngineEditor,

    // Appended so the existing values keep their order. Updating the audio last in the frame is
    // harmless: it only tops up streaming buffers and advances volume ramps.
    Audio
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