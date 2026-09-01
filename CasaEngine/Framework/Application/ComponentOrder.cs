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
    Audio,

    // The screen effect overlay must submit after the world/gameplay components have run their
    // Update (which is when the DLL pushes this frame's fade/tint state to the service) - see
    // CasaEngineGame.Update: GameManager.UpdateWorld runs before every GameComponent's Update, so
    // ordering only needs to place this after Audio, not before it.
    ScreenEffects
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