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

namespace CasaEngine.Core.Helpers
{

    public class Chronometer : Disposable
    {


        public enum TimeSpaceEnum
        {
            GameDeltaTime,
            FrameTime,
        } // TimeSpaceEnum



        private static readonly List<Chronometer> Chronometers = new();



        public double ElapsedTime { get; set; }

        public bool Paused { get; private set; }

        public TimeSpaceEnum TimeSpace { get; private set; }



        public Chronometer(TimeSpaceEnum timeSpace = TimeSpaceEnum.GameDeltaTime)
        {
            ElapsedTime = 0;
            Paused = true;
            TimeSpace = timeSpace;
            // If the application/game relies heavily in chronometers a pool should be used. TODO!!
            Chronometers.Add(this);
        } // Chronometer



        public void Start()
        {
            Paused = false;
        } // Start

        public void Pause()
        {
            Paused = true;
        } // Pause



        public void Reset()
        {
            ElapsedTime = 0;
        } // Reset



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



        protected override void DisposeManagedResources()
        {
            Chronometers.Remove(this);
        } // DisposeManagedResources



        public static void PauseAllChronometers()
        {
            foreach (var chronometer in Chronometers)
            {
                chronometer.Pause();
            }
        } // PauseAllChronometers

        public static void StartAllChronometers()
        {
            foreach (var chronometer in Chronometers)
            {
                chronometer.Start();
            }
        } // StartAllChronometers

        internal static void UpdateGameDeltaTimeChronometers()
        {
            foreach (var chronometer in Chronometers)
            {
                if (chronometer.TimeSpace == TimeSpaceEnum.GameDeltaTime)
                {
                    chronometer.Update();
                }
            }
        } // UpdateGameDeltaTimeChronometers

        internal static void UpdateFrameTimeChronometers()
        {
            foreach (var chronometer in Chronometers)
            {
                if (chronometer.TimeSpace == TimeSpaceEnum.FrameTime)
                {
                    chronometer.Update();
                }
            }
        } // UpdateFrameTimeChronometers


    } // Chronometer
} // XNAFinalEngine.Helpers
