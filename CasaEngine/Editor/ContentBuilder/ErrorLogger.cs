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


        private void ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            _errors.Add(e.Message);
        }


        public List<string> Errors => _errors;

        private readonly List<string> _errors = new();




        string ILogger.Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        private string _parameters;


        LoggerVerbosity ILogger.Verbosity
        {
            get => _verbosity;
            set => _verbosity = value;
        }

        private LoggerVerbosity _verbosity = LoggerVerbosity.Normal;


    }
}
