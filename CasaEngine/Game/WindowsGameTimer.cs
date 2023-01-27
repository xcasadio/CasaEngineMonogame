using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using CasaEngineCommon.Logger;

namespace CasaEngine.Game
{
    /// <summary>
    /// 
    /// </summary>
    class WindowsGameTimer
    {
        #region Using Statements
        private Stopwatch stopwatch;
        private TimeSpan lastElapsed;

        #endregion

        public WindowsGameTimer()
        {
            if (!Stopwatch.IsHighResolution)
            {
                LogManager.Instance.WriteLineWarning("Created " + this.GetType().FullName + ", but it is not high resolution. Maybe the underlying platform doesn't support high resolution timers?");
            }
            stopwatch = Stopwatch.StartNew();
            Reset();
        }

        public void Update()
        {
            TimeSpan elapsed = stopwatch.Elapsed;
            ElapsedTime = elapsed - lastElapsed;
            lastElapsed = elapsed;
        }

        public void Reset()
        {
            stopwatch.Restart();
            lastElapsed = stopwatch.Elapsed;
        }

        public void Suspend()
        {
            stopwatch.Stop();
        }

        public void Resume()
        {
            stopwatch.Start();
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

        public long Frequency
        {
            get
            {
                return Stopwatch.Frequency;
            }
        }

        public long Timestamp
        {
            get
            {
                return Stopwatch.GetTimestamp();
            }
        }
    }
}
