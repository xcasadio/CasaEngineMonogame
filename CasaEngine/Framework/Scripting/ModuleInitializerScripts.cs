using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Scripting;

internal static class ModuleInitializerScripts
{
    [ModuleInitializer]
    public static void Initialize()
    {
        GameSettings.ElementFactory.Register<ScriptArcBallCamera>(Guid.Parse("7CEA94AA-0FEC-45F4-A725-B6B83C17F7A6"));
    }
}