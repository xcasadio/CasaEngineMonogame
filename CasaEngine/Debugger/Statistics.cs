using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using XNAFinalEngine.Helpers;

namespace CasaEngine.Debugger
{
    /// <summary>
    /// Useful statistics to profile your game quickly.
    /// When you need to deeply profile your game use a specialized profiler.
    /// PIX, DirectX Debugger and Visual Studio’s profiler are powerful profilers.
    /// </summary>
    /// <remarks>
    /// The time measures (frame per seconds, update time and frame time) were left in the Time class for organization sake.
    /// The editor namespace includes a script to visualize the statistic on screen.
    /// </remarks>
    public static class Statistics
    {


        private static float framePerSecondTimeHelper;
        private static int frameCountThisSecond, drawCallThisSecond, trianglesDrawnThisSecond;



        /// <summary>
        /// The number of triangles processed in the current frame.
        /// </summary>
        public static int TrianglesDrawn { get; internal set; }

        /// <summary>
        /// An approximation of the average number of triangles processed in the last second.
        /// </summary>
        public static int AverageTrianglesDrawn { get; internal set; }

        /// <summary>
        /// An approximation of the number of vertices processed in the current frame.
        /// </summary>
        public static int VerticesProcessed { get; internal set; }

        /// <summary>
        /// An approximation of the number of draw calls performed in the current frame.
        /// </summary>
        /// <remarks>
        /// A draw call occurs every time you call one of the GraphicsDevice.Draw.
        /// When using Model, you get one draw call per mesh part.
        /// </remarks>
        public static int DrawCalls { get; internal set; }

        /// <summary>
        /// An approximation of the average number of draw calls performed in the last second.
        /// </summary>
        public static int AverageDrawCalls { get; internal set; }

        /// <summary>
        /// The number of garbage collections performed in execution time.
        /// </summary>
        public static int GarbageCollections { get; internal set; }

        /// <summary>
        /// Currently allocated memory in the managed heap.
        /// </summary>
        /// <remarks>
        /// If no C++ library is used then probably all the memory will be managed except the data allocated in the GPU.
        /// </remarks>
        public static long ManagedMemoryUsed { get { return GC.GetTotalMemory(false); } }



        /// <summary>
        /// Init the statistics. Call it after the first garbage collection.
        /// </summary>
        internal static void InitStatistics()
        {
            GarbageCollector.CreateWeakReference();
        } // InitStatistics



        /// <summary>
        /// Reset to 0 the values that are measured frame by frame.
        /// </summary>
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
