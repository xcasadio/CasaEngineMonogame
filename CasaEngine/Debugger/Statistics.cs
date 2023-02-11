using XNAFinalEngine.Helpers;

namespace CasaEngine.Debugger
{
    public static class Statistics
    {


        private static float _framePerSecondTimeHelper;
        private static int _frameCountThisSecond, _drawCallThisSecond, _trianglesDrawnThisSecond;



        public static int TrianglesDrawn { get; internal set; }

        public static int AverageTrianglesDrawn { get; internal set; }

        public static int VerticesProcessed { get; internal set; }

        public static int DrawCalls { get; internal set; }

        public static int AverageDrawCalls { get; internal set; }

        public static int GarbageCollections { get; internal set; }

        public static long ManagedMemoryUsed => GC.GetTotalMemory(false);


        internal static void InitStatistics()
        {
            GarbageCollector.CreateWeakReference();
        } // InitStatistics



        internal static void ReserFrameStatistics()
        {
            _drawCallThisSecond += DrawCalls;
            _trianglesDrawnThisSecond += TrianglesDrawn;
            // Update timer with the real elapsed time (not scaled)
            throw new NotImplementedException("Statistics.ReserFrameStatistics()");
            //framePerSecondTimeHelper += Time.FrameTime;
            // Update frame count.
            _frameCountThisSecond++;
            // One second elapsed?
            if (_framePerSecondTimeHelper > 1)
            {
                // Calculate frames per second
                AverageDrawCalls = _drawCallThisSecond / _frameCountThisSecond;
                AverageTrianglesDrawn = _trianglesDrawnThisSecond / _frameCountThisSecond;
                // Reset startSecondTick and repaintCountSecond
                _framePerSecondTimeHelper = 0;
                _frameCountThisSecond = 0;
                _drawCallThisSecond = 0;
                _trianglesDrawnThisSecond = 0;
            }

            TrianglesDrawn = 0;
            VerticesProcessed = 0;
            DrawCalls = 0;
        } // ReserFrameStatistics


    } // Statistics
}
