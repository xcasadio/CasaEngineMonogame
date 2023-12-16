using System.IO;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using CasaEngine.Framework.Game;

LogManager.Instance.AddLogger(new DebugLogger());
LogManager.Instance.AddLogger(new FileLogger("log.txt"));
LogManager.Instance.Verbosity = LogVerbosity.Trace;

EngineEnvironment.ProjectPath = Path.GetFullPath(Path.GetDirectoryName(args[0]));
using var game = new CasaEngineGame(args[0]);
game.Run();
