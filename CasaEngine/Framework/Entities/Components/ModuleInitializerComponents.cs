using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.Framework.Entities.Components;

internal static class ModuleInitializerComponents
{
    [ModuleInitializer]
    public static void Initialize()
    {
        GameSettings.ElementFactory.Register<AActor>(Guid.Parse("F439C545-71FB-4CF7-AD9D-F0CCCFA9942C"));

        GameSettings.ElementFactory.Register<AnimatedSpriteComponent>(Guid.Parse("5F7FE36D-DDFB-43C3-871B-05E9F2C1CD83"));
        GameSettings.ElementFactory.Register<ArcBallCameraComponent>(Guid.Parse("903DCF99-84AE-49D4-AAAB-5287FDBFEAC2"));
        GameSettings.ElementFactory.Register<CameraLookAtComponent>(Guid.Parse("97487909-BB7B-49A6-A877-338A3CDD9642"));
        GameSettings.ElementFactory.Register<CameraTargeted2dComponent>(Guid.Parse("8BAD0ED8-8C54-4A6A-A8EA-CAFFF821E251"));
        GameSettings.ElementFactory.Register<Camera3dIn2dAxisComponent>(Guid.Parse("C3B6AAA0-1B5D-476A-9601-088339E3EBC3"));
        GameSettings.ElementFactory.Register<SkinnedMeshComponent>(Guid.Parse("EF763450-E682-422F-B46E-11913974A627"));
        GameSettings.ElementFactory.Register<StaticMeshComponent>(Guid.Parse("FC007D02-51CE-4281-BCA8-1510D295F1F6"));
        GameSettings.ElementFactory.Register<Physics2dComponent>(Guid.Parse("0063AA3E-56D6-4386-9027-0B2F74E68FB7"));
        GameSettings.ElementFactory.Register<PhysicsComponent>(Guid.Parse("4464558A-17B8-409A-A9AC-927E802BA29D"));
        GameSettings.ElementFactory.Register<StaticSpriteComponent>(Guid.Parse("40C96394-5993-43A3-8800-9FFCAE812CD6"));
        GameSettings.ElementFactory.Register<TileMapComponent>(Guid.Parse("9425CB09-CC15-46B3-944A-CDECA5426170"));
        GameSettings.ElementFactory.Register<PlayerStartComponent>(Guid.Parse("ED867A33-2934-401A-821F-CF9F748AF6D4"));
    }
}