namespace CasaEngine.Framework.Game;

//For reorder updateable component
public enum ComponentUpdateOrder
{
    GameManager = 500,
    Renderer2dComponent,
    Renderer3dComponent,
    Line3dComponent,
    MeshComponent,
    SkinnedMeshComponent,
    Input = 510,
    Manipulator,
    DebugManager = 20,
    ScreenLogComponent = 526,
    ScreenManagerComponent = 530,
    ViewComponent = 540,
    Physics = 545,
    Gameplay = 560,
    ParticleComponent = 570,
    SpacingComponent = 580,
    DebugPhysics,
    Default = 5100

#if EDITOR
    , CasaEngineEditor = 51000
#endif
}

//For reorder drawable component
public enum ComponentDrawOrder
{
    GameManager = 500,
    DebugManager = 501,
    Input = 510,
    ScreenManagerComponent = 530,
    Gameplay = 551,
    ParticleComponent = 554,
    ScreenLogComponent = 556,
    Default = 5100,
    MeshComponent,
    SkinnedMeshComponent,
    Renderer2dComponent = 5150,
    Renderer3dComponent,
    Line3dComponent,
    DebugPhysics = 5200,
    Manipulator = 51000
}