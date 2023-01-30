using XNAFinalEngine.Helpers;

namespace CasaEngine.Debugger
{
    public static class Statistics
    {


        private static float framePerSecondTimeHelper;
        private static int frameCountThisSecond, drawCallThisSecond, trianglesDrawnThisSecond;



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
            drawCallThisSecond += DrawCalls;
            trianglesDrawnThisSecond += TrianglesDrawn;
            // Update timer with the real elapsed time (not scaled)
            throw new NotImplementedException("Statistics.ReserFrameStatistics()");
            //framePerSecondTimeHelper += Time.FrameTime;
            // Update frame count.
            frameCountThisSecond++;
            // One second elapsed?
            if (framePerSecondTimeHelper > 1)
            {
                // Calculate frames per second
                AverageDrawCalls = drawCallThisSecond / frameCountThisSecond;
                AverageTrianglesDrawn = trianglesDrawnThisSecond / frameCountThisSecond;
                // Reset startSecondTick and repaintCountSecond
                framePerSecondTimeHelper = 0;
                frameCountThisSecond = 0;
                drawCallThisSecond = 0;
                trianglesDrawnThisSecond = 0;
            }

            TrianglesDrawn = 0;
            VerticesProcessed = 0;
            DrawCalls = 0;
        } // ReserFrameStatistics


    } // Statistics
}
