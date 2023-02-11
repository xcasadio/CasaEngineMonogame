using System.Diagnostics;
using CasaEngineCommon.Logger;

namespace CasaEngine.Game
{
    class WindowsGameTimer
    {
        private readonly Stopwatch _stopwatch;
        private TimeSpan _lastElapsed;


        public WindowsGameTimer()
        {
            if (!Stopwatch.IsHighResolution)
            {
                LogManager.Instance.WriteLineWarning("Created " + this.GetType().FullName + ", but it is not high resolution. Maybe the underlying platform doesn't support high resolution timers?");
            }
            _stopwatch = Stopwatch.StartNew();
            Reset();
        }

        public void Update()
        {
            TimeSpan elapsed = _stopwatch.Elapsed;
            ElapsedTime = elapsed - _lastElapsed;
            _lastElapsed = elapsed;
        }

        public void Reset()
        {
            _stopwatch.Restart();
            _lastElapsed = _stopwatch.Elapsed;
        }

        public void Suspend()
        {
            _stopwatch.Stop();
        }

        public void Resume()
        {
            _stopwatch.Start();
        }

        public TimeSpan ElapsedTime
        {
            get;
            internal set;
        }

        public TimeSpan CurrentTime
        {
            get;
            internal set;
        }

        public long Frequency => Stopwatch.Frequency;

        public long Timestamp => Stopwatch.GetTimestamp();
    }
}
