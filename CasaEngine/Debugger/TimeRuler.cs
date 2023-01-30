//-----------------------------------------------------------------------------
// TimeRuler.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System.Diagnostics;
using Microsoft.Xna.Framework;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;
#if EDITOR
//using CasaEngine.Editor.GameComponent;
#endif


namespace CasaEngine.Debugger
{
    public class TimeRuler
        : Microsoft.Xna.Framework.DrawableGameComponent, IGameComponentResizable
    {

        const int MaxBars = 8;

        const int MaxSamples = 256;

        const int MaxNestCall = 32;

        const int MaxSampleFrames = 4;

        const int LogSnapDuration = 120;

        const int BarHeight = 8;

        const int BarPadding = 2;

        const int AutoAdjustDelay = 30;



        public bool CanSetVisible => false;

        public bool CanSetEnable => true;

        public bool ShowLog { get; set; }

        public int TargetSampleFrames { get; set; }

        public Vector2 Position
        {
            get => position;
            set => position = value;
        }

        public int Width { get; set; }



#if TRACE

        private struct Marker
        {
            public int MarkerId;
            public float BeginTime;
            public float EndTime;
            public Color Color;
        }

        private class MarkerCollection
        {
            // Marker collection.
            public Marker[] Markers = new Marker[MaxSamples];
            public int MarkCount;

            // Marker nest information.
            public int[] MarkerNests = new int[MaxNestCall];
            public int NestCount;
        }

        private class FrameLog
        {
            public MarkerCollection[] Bars;

            public FrameLog()
            {
                // Initialize markers.
                Bars = new MarkerCollection[MaxBars];
                for (int i = 0; i < MaxBars; ++i)
                    Bars[i] = new MarkerCollection();
            }
        }

        private class MarkerInfo
        {
            // Name of marker.
            public string Name;

            // Marker log.
            public MarkerLog[] Logs = new MarkerLog[MaxBars];

            public MarkerInfo(string name)
            {
                Name = name;
            }
        }

        private struct MarkerLog
        {
            public float SnapMin;
            public float SnapMax;
            public float SnapAvg;

            public float Min;
            public float Max;
            public float Avg;

            public int Samples;

            public Color Color;

            public bool Initialized;
        }

        // Reference of debug manager.
        DebugManager debugManager;

        // Logs for each frames.
        FrameLog[] logs;

        // Previous frame log.
        FrameLog prevLog;

        // Current log.
        FrameLog curLog;

        // Current frame count.
        int frameCount;

        // Stopwatch for measure the time.
        Stopwatch stopwatch = new Stopwatch();

        // Marker information array.
        List<MarkerInfo> markers = new List<MarkerInfo>();

        // Dictionary that maps from marker name to marker id.
        Dictionary<string, int> markerNameToIdMap = new Dictionary<string, int>();

        // Display frame adjust counter.
        int frameAdjust;

        // Current display frame count.
        int sampleFrames;

        // Marker log string.
        StringBuilder logString = new StringBuilder(512);

        // You want to call StartFrame at beginning of Game.Update method.
        // But Game.Update gets calls multiple time when game runs slow in fixed time step mode.
        // In this case, we should ignore StartFrame call.
        // To do this, we just keep tracking of number of StartFrame calls until Draw gets called.
        int updateCount;

#endif
        // TimerRuler draw position.
        Vector2 position;

        Renderer2DComponent m_Renderer2DComponent = null;

        Color m_BackgroundColor = new Color(0, 0, 0, 128);



        public TimeRuler(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            // Add this as a service.
            Game.Services.AddService(typeof(TimeRuler), this);

            UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
            DrawOrder = (int)ComponentDrawOrder.DebugManager;
        }

        public override void Initialize()
        {
#if TRACE
            debugManager =
                Game.Services.GetService(typeof(DebugManager)) as DebugManager;

            if (debugManager == null)
                throw new InvalidOperationException("DebugManager is not registered.");

            // Add "tr" command if DebugCommandHost is registered.
            IDebugCommandHost host =
                                Game.Services.GetService(typeof(IDebugCommandHost))
                                                                    as IDebugCommandHost;
            if (host != null)
            {
                host.RegisterCommand("tr", "TimeRuler", this.CommandExecute);
                this.Visible = false;
            }

            // Initialize Parameters.
            logs = new FrameLog[2];
            for (int i = 0; i < logs.Length; ++i)
                logs[i] = new FrameLog();

            sampleFrames = TargetSampleFrames = 1;

            // Time-Ruler's update method doesn't need to get called.
            this.Enabled = false;
#endif

            base.Initialize();
        }

        protected override void LoadContent()
        {
            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Game);

            if (m_Renderer2DComponent == null)
            {
                throw new InvalidOperationException("TimeRuler.LoadContent() : Renderer2DComponent is null");
            }

            OnResize();

            base.LoadContent();
        }

#if TRACE
        void CommandExecute(IDebugCommandHost host, string command,
                                                                IList<string> arguments)
        {
            bool previousVisible = Visible;

            if (arguments.Count == 0)
                Visible = !Visible;

            char[] subArgSeparator = new[] { ':' };
            foreach (string orgArg in arguments)
            {
                string arg = orgArg.ToLower();
                string[] subargs = arg.Split(subArgSeparator);
                switch (subargs[0])
                {
                    case "on":
                        Visible = true;
                        break;
                    case "off":
                        Visible = false;
                        break;
                    case "reset":
                        ResetLog();
                        break;
                    case "log":
                        if (subargs.Length > 1)
                        {
                            if (String.Compare(subargs[1], "on") == 0)
                                ShowLog = true;
                            if (String.Compare(subargs[1], "off") == 0)
                                ShowLog = false;
                        }
                        else
                        {
                            ShowLog = !ShowLog;
                        }
                        break;
                    case "frame":
                        int a = Int32.Parse(subargs[1]);
                        a = System.Math.Max(a, 1);
                        a = System.Math.Min(a, MaxSampleFrames);
                        TargetSampleFrames = a;
                        break;
                    case "/?":
                    case "--help":
                        host.Echo("tr [log|on|off|reset|frame]");
                        host.Echo("Options:");
                        host.Echo("       on     Display TimeRuler.");
                        host.Echo("       off    Hide TimeRuler.");
                        host.Echo("       log    Show/Hide marker log.");
                        host.Echo("       reset  Reset marker log.");
                        host.Echo("       frame:sampleFrames");
                        host.Echo("              Change target sample frame count");
                        break;
                    default:
                        break;
                }
            }

            // Reset update count when Visible state changed.
            if (Visible != previousVisible)
            {
                Interlocked.Exchange(ref updateCount, 0);
            }
        }
#endif

        public void OnResize()
        {
            Width = (int)(GraphicsDevice.Viewport.Width * 0.8f);

            Layout layout = new Layout(GraphicsDevice.Viewport);
            position = layout.Place(new Vector2(Width, BarHeight),
                                                    0, 0.01f, Alignment.BottomCenter);
        }



        [Conditional("TRACE")]
        public void StartFrame()
        {
#if TRACE
            lock (this)
            {
                // We skip reset frame when this method gets called multiple times.
                int count = Interlocked.Increment(ref updateCount);
                if (Visible && (1 < count && count < MaxSampleFrames))
                    return;

                // Update current frame log.
                prevLog = logs[frameCount++ & 0x1];
                curLog = logs[frameCount & 0x1];

                float endFrameTime = (float)stopwatch.Elapsed.TotalMilliseconds;

                // Update marker and create a log.
                for (int barIdx = 0; barIdx < prevLog.Bars.Length; ++barIdx)
                {
                    MarkerCollection prevBar = prevLog.Bars[barIdx];
                    MarkerCollection nextBar = curLog.Bars[barIdx];

                    // Re-open marker that didn't get called EndMark in previous frame.
                    for (int nest = 0; nest < prevBar.NestCount; ++nest)
                    {
                        int markerIdx = prevBar.MarkerNests[nest];

                        prevBar.Markers[markerIdx].EndTime = endFrameTime;

                        nextBar.MarkerNests[nest] = nest;
                        nextBar.Markers[nest].MarkerId =
                            prevBar.Markers[markerIdx].MarkerId;
                        nextBar.Markers[nest].BeginTime = 0;
                        nextBar.Markers[nest].EndTime = -1;
                        nextBar.Markers[nest].Color = prevBar.Markers[markerIdx].Color;
                    }

                    // Update marker log.
                    for (int markerIdx = 0; markerIdx < prevBar.MarkCount; ++markerIdx)
                    {
                        float duration = prevBar.Markers[markerIdx].EndTime -
                                            prevBar.Markers[markerIdx].BeginTime;

                        int markerId = prevBar.Markers[markerIdx].MarkerId;
                        MarkerInfo m = markers[markerId];

                        m.Logs[barIdx].Color = prevBar.Markers[markerIdx].Color;

                        if (!m.Logs[barIdx].Initialized)
                        {
                            // First frame process.
                            m.Logs[barIdx].Min = duration;
                            m.Logs[barIdx].Max = duration;
                            m.Logs[barIdx].Avg = duration;

                            m.Logs[barIdx].Initialized = true;
                        }
                        else
                        {
                            // Process after first frame.
                            m.Logs[barIdx].Min = System.Math.Min(m.Logs[barIdx].Min, duration);
                            m.Logs[barIdx].Max = System.Math.Min(m.Logs[barIdx].Max, duration);
                            m.Logs[barIdx].Avg += duration;
                            m.Logs[barIdx].Avg *= 0.5f;

                            if (m.Logs[barIdx].Samples++ >= LogSnapDuration)
                            {
                                m.Logs[barIdx].SnapMin = m.Logs[barIdx].Min;
                                m.Logs[barIdx].SnapMax = m.Logs[barIdx].Max;
                                m.Logs[barIdx].SnapAvg = m.Logs[barIdx].Avg;
                                m.Logs[barIdx].Samples = 0;
                            }
                        }
                    }

                    nextBar.MarkCount = prevBar.NestCount;
                    nextBar.NestCount = prevBar.NestCount;
                }

                // Start measuring.
                stopwatch.Reset();
                stopwatch.Start();
            }
#endif
        }

        [Conditional("TRACE")]
        public void BeginMark(string markerName, Color color)
        {
#if TRACE
            BeginMark(0, markerName, color);
#endif
        }

        [Conditional("TRACE")]
        public void BeginMark(int barIndex, string markerName, Color color)
        {
#if TRACE
            lock (this)
            {
                if (barIndex < 0 || barIndex >= MaxBars)
                    throw new ArgumentOutOfRangeException("barIndex");

                MarkerCollection bar = curLog.Bars[barIndex];

                if (bar.MarkCount >= MaxSamples)
                {
                    throw new OverflowException(
                        "Exceeded sample count.\n" +
                        "Either set larger number to TimeRuler.MaxSmpale or" +
                        "lower sample count.");
                }

                if (bar.NestCount >= MaxNestCall)
                {
                    throw new OverflowException(
                        "Exceeded nest count.\n" +
                        "Either set larget number to TimeRuler.MaxNestCall or" +
                        "lower nest calls.");
                }

                // Gets registered marker.
                int markerId;
                if (!markerNameToIdMap.TryGetValue(markerName, out markerId))
                {
                    // Register this if this marker is not registered.
                    markerId = markers.Count;
                    markerNameToIdMap.Add(markerName, markerId);
                    markers.Add(new MarkerInfo(markerName));
                }

                // Start measuring.
                bar.MarkerNests[bar.NestCount++] = bar.MarkCount;

                // Fill marker parameters.
                bar.Markers[bar.MarkCount].MarkerId = markerId;
                bar.Markers[bar.MarkCount].Color = color;
                bar.Markers[bar.MarkCount].BeginTime =
                                        (float)stopwatch.Elapsed.TotalMilliseconds;

                bar.Markers[bar.MarkCount].EndTime = -1;

                bar.MarkCount++;
            }
#endif
        }

        [Conditional("TRACE")]
        public void EndMark(string markerName)
        {
#if TRACE
            EndMark(0, markerName);
#endif
        }

        [Conditional("TRACE")]
        public void EndMark(int barIndex, string markerName)
        {
#if TRACE
            lock (this)
            {
                if (barIndex < 0 || barIndex >= MaxBars)
                    throw new ArgumentOutOfRangeException("barIndex");

                MarkerCollection bar = curLog.Bars[barIndex];

                if (bar.NestCount <= 0)
                {
                    throw new InvalidOperationException(
                        "Call BeingMark method before call EndMark method.");
                }

                int markerId;
                if (!markerNameToIdMap.TryGetValue(markerName, out markerId))
                {
                    throw new InvalidOperationException(
                        String.Format("Maker '{0}' is not registered." +
                            "Make sure you specifed same name as you used for BeginMark" +
                            " method.",
                            markerName));
                }

                int markerIdx = bar.MarkerNests[--bar.NestCount];
                if (bar.Markers[markerIdx].MarkerId != markerId)
                {
                    throw new InvalidOperationException(
                    "Incorrect call order of BeginMark/EndMark method." +
                    "You call it like BeginMark(A), BeginMark(B), EndMark(B), EndMark(A)" +
                    " But you can't call it like " +
                    "BeginMark(A), BeginMark(B), EndMark(A), EndMark(B).");
                }

                bar.Markers[markerIdx].EndTime =
                    (float)stopwatch.Elapsed.TotalMilliseconds;
            }
#endif
        }

        public float GetAverageTime(int barIndex, string markerName)
        {
#if TRACE
            if (barIndex < 0 || barIndex >= MaxBars)
                throw new ArgumentOutOfRangeException("barIndex");

            float result = 0;
            int markerId;
            if (markerNameToIdMap.TryGetValue(markerName, out markerId))
                result = markers[markerId].Logs[barIndex].Avg;

            return result;
#else
            return 0f;
#endif
        }

        [Conditional("TRACE")]
        public void ResetLog()
        {
#if TRACE
            lock (this)
            {
                foreach (MarkerInfo markerInfo in markers)
                {
                    for (int i = 0; i < markerInfo.Logs.Length; ++i)
                    {
                        markerInfo.Logs[i].Initialized = false;
                        markerInfo.Logs[i].SnapMin = 0;
                        markerInfo.Logs[i].SnapMax = 0;
                        markerInfo.Logs[i].SnapAvg = 0;

                        markerInfo.Logs[i].Min = 0;
                        markerInfo.Logs[i].Max = 0;
                        markerInfo.Logs[i].Avg = 0;

                        markerInfo.Logs[i].Samples = 0;
                    }
                }
            }
#endif
        }



        public override void Draw(GameTime gameTime)
        {
            Draw(position, Width);
            base.Draw(gameTime);
        }

        [Conditional("TRACE")]
        public void Draw(Vector2 position, int width)
        {
#if TRACE
            // Reset update count.
            Interlocked.Exchange(ref updateCount, 0);

			SpriteFont font = Engine.Instance.DefaultSpriteFont;//debugManager.DebugFont;
            Texture2D texture = debugManager.WhiteTexture;
            float depth_ = 0.0f;

            // Adjust size and position based of number of bars we should draw.
            int height = 0;
            float maxTime = 0;
            foreach (MarkerCollection bar in prevLog.Bars)
            {
                if (bar.MarkCount > 0)
                {
                    height += BarHeight + BarPadding * 2;
                    maxTime = System.Math.Max(maxTime,
                                            bar.Markers[bar.MarkCount - 1].EndTime);
                }
            }

            // Auto display frame adjustment.
            // For example, if the entire process of frame doesn't finish in less than 16.6ms
            // thin it will adjust display frame duration as 33.3ms.
            const float frameSpan = 1.0f / 60.0f * 1000f;
            float sampleSpan = (float)sampleFrames * frameSpan;

            if (maxTime > sampleSpan)
                frameAdjust = System.Math.Max(0, frameAdjust) + 1;
            else
                frameAdjust = System.Math.Min(0, frameAdjust) - 1;

            if (System.Math.Abs(frameAdjust) > AutoAdjustDelay)
            {
                sampleFrames = System.Math.Min(MaxSampleFrames, sampleFrames);
                sampleFrames =
                    System.Math.Max(TargetSampleFrames, (int)(maxTime / frameSpan) + 1);

                frameAdjust = 0;
            }

            // Compute factor that converts from ms to pixel.
            float msToPs = (float)width / sampleSpan;

            // Draw start position.
            int startY = (int)position.Y - (height - BarHeight);

            // Current y position.
            int y = startY;

            // Draw transparency background.
            Rectangle rc = new Rectangle((int)position.X, y, width, height);
            m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, m_BackgroundColor, depth_ + 0.09f, SpriteEffects.None);

            // Draw markers for each bars.
            rc.Height = BarHeight;
            foreach (MarkerCollection bar in prevLog.Bars)
            {
                rc.Y = y + BarPadding;
                if (bar.MarkCount > 0)
                {
                    for (int j = 0; j < bar.MarkCount; ++j)
                    {
                        float bt = bar.Markers[j].BeginTime;
                        float et = bar.Markers[j].EndTime;
                        int sx = (int)(position.X + bt * msToPs);
                        int ex = (int)(position.X + et * msToPs);
                        rc.X = sx;
                        rc.Width = System.Math.Max(ex - sx, 1);

                        m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, bar.Markers[j].Color, depth_ + 0.08f, SpriteEffects.None);
                    }
                }

                y += BarHeight + BarPadding;
            }

            // Draw grid lines.
            // Each grid represents ms.
            rc = new Rectangle((int)position.X, (int)startY, 1, height);
            for (float t = 1.0f; t < sampleSpan; t += 1.0f)
            {
                rc.X = (int)(position.X + t * msToPs);
                m_Renderer2DComponent.AddSprite2D(texture, rc,  Point.Zero, Vector2.Zero, 0.0f, Vector2.One,Color.Gray, depth_ + 0.07f, SpriteEffects.None);
            }

            // Draw frame grid.
            for (int i = 0; i <= sampleFrames; ++i)
            {
                rc.X = (int)(position.X + frameSpan * (float)i * msToPs);
                m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, Color.White, depth_ + 0.6f, SpriteEffects.None);
            }

            // Draw log.
            if (ShowLog)
            {
                // Generate log string.
                y = startY - font.LineSpacing;
                logString.Length = 0;
                foreach (MarkerInfo markerInfo in markers)
                {
                    for (int i = 0; i < MaxBars; ++i)
                    {
                        if (markerInfo.Logs[i].Initialized)
                        {
                            if (logString.Length > 0)
                                logString.Append("\n");

                            logString.Append(" Bar ");
                            logString.AppendNumber(i);
                            logString.Append(" ");
                            logString.Append(markerInfo.Name);

                            logString.Append(" Avg.:");
                            logString.AppendNumber(markerInfo.Logs[i].SnapAvg);
                            logString.Append("ms ");

                            y -= font.LineSpacing;
                        }
                    }
                }

                // Compute background size and draw it.
                Vector2 size = font.MeasureString(logString);
                rc = new Rectangle((int)position.X, (int)y, (int)size.X + 12, (int)size.Y);
                m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, m_BackgroundColor, depth_ + 0.5f, SpriteEffects.None);

                // Draw log string.
				m_Renderer2DComponent.AddText2D(font, logString.ToString(),
										new Vector2(position.X + 12, y), 0.0f,
                                        Vector2.One, Color.White, depth_);

                // Draw log color boxes.
                y += (int)((float)font.LineSpacing * 0.3f);
                rc = new Rectangle((int)position.X + 4, y, 10, 10);
                Rectangle rc2 = new Rectangle((int)position.X + 5, y + 1, 8, 8);
                foreach (MarkerInfo markerInfo in markers)
                {
                    for (int i = 0; i < MaxBars; ++i)
                    {
                        if (markerInfo.Logs[i].Initialized)
                        {
                            rc.Y = y;
                            rc2.Y = y + 1;
                            m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, Color.White, depth_, SpriteEffects.None);
                            m_Renderer2DComponent.AddSprite2D(texture, rc, Point.Zero, Vector2.Zero, 0.0f, Vector2.One, markerInfo.Logs[i].Color, depth_, SpriteEffects.None);

                            y += font.LineSpacing;
                        }
                    }
                }
            }
#endif
        }

    }
}
