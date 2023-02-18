using Microsoft.Build.Framework;

namespace CasaEngine.Editor.ContentBuilder
{
    public class ErrorLogger : ILogger
    {
        public void Initialize(IEventSource eventSource)
        {
            if (eventSource != null)
            {
                eventSource.ErrorRaised += ErrorRaised;
            }
        }
        public void Shutdown()
        {
        }


        void ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            _errors.Add(e.Message);
        }


        public List<string> Errors => _errors;

        readonly List<string> _errors = new();




        string ILogger.Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        string _parameters;


        LoggerVerbosity ILogger.Verbosity
        {
            get => _verbosity;
            set => _verbosity = value;
        }

        LoggerVerbosity _verbosity = LoggerVerbosity.Normal;


    }
}
