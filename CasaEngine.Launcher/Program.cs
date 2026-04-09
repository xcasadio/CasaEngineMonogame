using System;
using System.IO;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.UI;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Logs.AddLogger(new DebugLogger());
        Logs.AddLogger(new FileLogger("log.txt"));
        Logs.Verbosity = LogVerbosity.Trace;

        EngineEnvironment.ProjectPath = Path.GetFullPath(Path.GetDirectoryName(args[0]));
        var runtimeContext = GameSettings.CreateRuntimeContext();
        runtimeContext.UIViewRuntimeFactory = new MguiViewRuntimeFactory();

        using var game = new CasaEngineGame(args[0], runtimeContext: runtimeContext);
        game.Run();
    }
}