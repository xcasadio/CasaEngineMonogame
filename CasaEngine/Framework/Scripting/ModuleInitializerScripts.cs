using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Scripting;

internal static class ModuleInitializerScripts
{
    [ModuleInitializer]
    public static void Initialize()
    {
        //GameSettings.ScriptLoader.Register<ScriptArcBallCamera>(new ScriptArcBallCamera().ExternalComponentId);
    }
}