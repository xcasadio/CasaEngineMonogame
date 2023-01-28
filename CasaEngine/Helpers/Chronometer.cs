
/*
Copyright (c) 2008-2011, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System.Collections.Generic;
using System;




namespace XNAFinalEngine.Helpers
{

    /// <summary>
    /// Chronometer in seconds, starts in pause.
    /// They are updated automatically and by default use the engine's game delta time (update time).
    /// </summary>
    /// <remarks>
    /// There are no events in this class because all code executes in a defined order. Events could destroy that order.
    /// However, you can use events in your own code because that won’t mess with the engine execution path.
    /// </remarks>
    public class Chronometer : Disposable
    {


        /// <summary>
        /// The chronometer could work in update time or frame time.
        /// </summary>
        public enum TimeSpaceEnum
        {
            /// <summary>
            /// Uses the interval in seconds at which physics and other fixed frame rate updates.
            /// </summary>
            GameDeltaTime,
            /// <summary>
            /// Uses the rendered frame time.
            /// </summary>
            FrameTime,
        } // TimeSpaceEnum



        /// <summary>
        /// The current chronometers in use by the aplication.
        /// </summary>
        private static readonly List<Chronometer> chronometers = new List<Chronometer>();



        /// <summary>
        /// Elapsed time in seconds.
        /// </summary>
        public double ElapsedTime { get; set; }

        /// <summary>
        /// It indicates if the chronometer is running or not. Pause is the default state.
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Indicates in witch space works (in update time or frame time)
        /// </summary>
        /// <value>Default: update time.</value>
        public TimeSpaceEnum TimeSpace { get; private set; }



        /// <summary>
        /// Chronometer in seconds, starts in pause.
        /// </summary>
        /// <param name="timeSpace">Indicates in witch space works (in update time or frame time). Default: update time.</param>
        public Chronometer(TimeSpaceEnum timeSpace = TimeSpaceEnum.GameDeltaTime)
        {
            ElapsedTime = 0;
            Paused = true;
            TimeSpace = timeSpace;
            // If the application/game relies heavily in chronometers a pool should be used. TODO!!
            chronometers.Add(this);
        } // Chronometer



        /// <summary>
        /// Resume chronometer.
        /// </summary>
        public void Start()
        {
            Paused = false;
        } // Start

        /// <summary>
        /// Pause chronometer.
        /// </summary>
        public void Pause()
        {
            Paused = true;
        } // Pause



        /// <summary>
        /// Reset counter. The state doesn't change.
        /// </summary>
        public void Reset()
        {
            ElapsedTime = 0;
        } // Reset



        /// <summary>
        /// Update.
        /// </summary>
        private void Update()
        {
            throw new NotImplementedException("Chronometer.Update()");
            /*if (!Paused)
            {
                if (TimeSpace == TimeSpaceEnum.FrameTime)
                    ElapsedTime += Time.FrameTime;
                else
                    ElapsedTime += Time.GameDeltaTime;
            }*/
        } // Update



        /// <summary>
        /// Dispose Managed Resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            chronometers.Remove(this);
        } // DisposeManagedResources



        /// <summary>
        /// Pause all chronometers.
        /// </summary>
        public static void PauseAllChronometers()
        {
            foreach (Chronometer chronometer in chronometers)
            {
                chronometer.Pause();
            }
        } // PauseAllChronometers

        /// <summary>
        /// Resume all chronometers.
        /// </summary>
        public static void StartAllChronometers()
        {
            foreach (Chronometer chronometer in chronometers)
            {
                chronometer.Start();
            }
        } // StartAllChronometers

        /// <summary>
        /// Update the chronometers that work in game delta time space.
        /// </summary>
        internal static void UpdateGameDeltaTimeChronometers()
        {
            foreach (Chronometer chronometer in chronometers)
            {
                if (chronometer.TimeSpace == TimeSpaceEnum.GameDeltaTime)
                    chronometer.Update();
            }
        } // UpdateGameDeltaTimeChronometers

        /// <summary>
        /// Update the chronometers that work in frame time space.
        /// </summary>
        internal static void UpdateFrameTimeChronometers()
        {
            foreach (Chronometer chronometer in chronometers)
            {
                if (chronometer.TimeSpace == TimeSpaceEnum.FrameTime)
                    chronometer.Update();
            }
        } // UpdateFrameTimeChronometers


    } // Chronometer
} // XNAFinalEngine.Helpers
