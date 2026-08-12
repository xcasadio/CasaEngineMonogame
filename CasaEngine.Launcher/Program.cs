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

        //var projectFileName = @"D:\development\repo\alundra-casaengine-project-converter\alundra-project\AlundraGame.json";//args[0];
        var projectFileName = args[0];

        EngineEnvironment.ProjectPath = Path.GetFullPath(Path.GetDirectoryName(projectFileName));
        var runtimeContext = GameSettings.CreateRuntimeContext();
        runtimeContext.UIViewRuntimeFactory = new MguiViewRuntimeFactory();

        using var game = new CasaEngineGame(projectFileName, runtimeContext: runtimeContext);
        game.Run();
    }
}