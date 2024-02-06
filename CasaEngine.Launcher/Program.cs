using System.IO;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Framework.Game;

Logs.AddLogger(new DebugLogger());
Logs.AddLogger(new FileLogger("log.txt"));
Logs.Verbosity = LogVerbosity.Trace;

EngineEnvironment.ProjectPath = Path.GetFullPath(Path.GetDirectoryName(args[0]));
using var game = new CasaEngineGame(args[0]);
game.Run();
