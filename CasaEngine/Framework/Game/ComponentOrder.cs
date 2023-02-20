namespace CasaEngine.Framework.Game
{
    //For reorder updateable component
    public enum ComponentUpdateOrder
    {
        GameManager = 500,
        DebugPhysics,
        Renderer2DComponent,
        MeshComponent,
        PickBuffer,
        Input = 510,
        Manipulator,
        DebugManager = 20,
        ScreenLogComponent = 526,
        ScreenManagerComponent = 530,
        ViewComponent = 540,
        PhysicEngine = 545,
        Gameplay = 560,
        ParticleComponent = 570,
        SpacingComponent = 580,
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
        Renderer2DComponent = 5150,
        DebugPhysics = 5200,
        Manipulator = 51000
    }
}