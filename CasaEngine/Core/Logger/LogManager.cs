using System.Text;

namespace CasaEngine.Core.Logger
{
    public sealed class LogManager
    {
        public enum LogVerbosity
        {
            Debug,
            Normal,
            None
        }

        private static LogManager? _instance;
        private readonly List<ILog> _loggers = new();
        private LogVerbosity _verbosity = LogVerbosity.Debug;

        public static LogManager Instance
        {
            get { return _instance ??= new LogManager(); }
        }

        public LogVerbosity Verbosity
        {
            get => _verbosity;
            set => _verbosity = value;
        }

        public void AddLogger(ILog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _loggers.Add(log);
        }

        public void Close()
        {
            foreach (var log in _loggers)
            {
                log.Close();
            }

            _loggers.Clear();
        }

        public void Write(params object[] args)
        {
            foreach (var log in _loggers)
            {
                log.Write(args);
            }
        }

        public void WriteLine(params object[] args)
        {
            object[] newArgs;

            if (args != null)
            {
                newArgs = new object[args.Length + 1];

                for (var i = 0; i < args.Length; i++)
                {
                    newArgs[i] = args[i];
                }
            }
            else
            {
                newArgs = new object[1];
            }

            newArgs[^1] = Environment.NewLine;

            Write(newArgs);
        }

        public void WriteLineDebug(string msg)
        {
            if (_verbosity != LogVerbosity.Debug)
            {
                return;
            }

            foreach (var log in _loggers)
            {
                log.WriteLineDebug(msg);
            }
        }

        public void WriteLineWarning(string msg)
        {
            if (_verbosity == LogVerbosity.None)
            {
                return;
            }

            foreach (var log in _loggers)
            {
                log.WriteLineWarning(msg);
            }
        }

        public void WriteLineError(string msg)
        {
            if (_verbosity == LogVerbosity.None)
            {
                return;
            }

            foreach (var log in _loggers)
            {
                log.WriteLineError(msg);
            }
        }

        public void WriteException(Exception e, bool writeStackTrace = true)
        {
            if (_verbosity == LogVerbosity.None)
            {
                return;
            }

            var message = new StringBuilder();

            message.AppendLine(e.Message);

            var ex = e;
            var tab = "\t";

            while (ex.InnerException != null)
            {
                message.Append(tab);
                message.AppendLine(ex.Message);
                ex = ex.InnerException;
                tab += "\t";
            }

            if (writeStackTrace)
            {
                message.AppendLine(e.StackTrace);
            }

            WriteLineError(message.ToString());
        }
    }
}
