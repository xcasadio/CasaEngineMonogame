using Microsoft.Build.Framework;

namespace CasaEngine.Editor.Builder
{
    class ErrorLogger : ILogger
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
            errors.Add(e.Message);
        }


        public List<string> Errors => errors;

        readonly List<string> errors = new List<string>();




        string ILogger.Parameters
        {
            get => parameters;
            set => parameters = value;
        }

        string parameters;


        LoggerVerbosity ILogger.Verbosity
        {
            get => verbosity;
            set => verbosity = value;
        }

        LoggerVerbosity verbosity = LoggerVerbosity.Normal;


    }
}
