
/*
Copyright (c) 2008-2013, Schneider, José Ignacio.
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
Author: Schneider, José Ignacio (jischneider@hotmail.com)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System;



namespace XNAFinalEngine.Helpers
{
    /// <summary>
    /// This indicates the available processors and the desired processors' affinity.
    /// </summary>
    /// <remarks>
    /// Threading in XNA Final Engine is fairly simple. 
    /// Thanks to the data oriented design, the task could be performed in serial mode spreading the work into different cores and therefore avoiding asynchronic updates.
    /// 
    /// The game loop asks for the hardware threads available and divides the work in them. 
    /// In Xbox 360 the engine uses the core 1, 3, 4 and 5; 0 and 2 are used by XNA (or the operative system).
    /// In PC, there is a hardware thread for each available core. I assume that the cores are not being used;
    /// however, the core that runs the application makes a little more work than the others to avoid a potential bottleneck,
    /// moreover, small test performed shows me that this is better for performance. 
    /// </remarks>
    public static class ProcessorsInformation
    {


        /// <summary>
        /// The number of processors availables. 
        /// The application thread is not included.
        /// </summary>
        public static int AvailableProcessors { get; private set; }

        /// <summary>
        /// The desired affinity of the Xbox 360 hardware threads. This not include the application thread.
        /// </summary>
        public static int[] ProcessorsAffinity { get; private set; }



        /// <summary>
        /// Obtain the number of logical cores available for multithreading.
        /// </summary>
        static ProcessorsInformation()
        {
#if XBOX
                // http://msdn.microsoft.com/en-us/library/microsoft.xna.net_cf.system.threading.thread.setprocessoraffinity.aspx
                AvailableProcessors = 3;
                ProcessorsAffinity = new [] { 3, 4, 5 };
#else
            AvailableProcessors = Environment.ProcessorCount - 1; // Minus the application thread.
#endif
        } // ProcessorsInformation


    } // ProcessorsInformation
} // XNAFinalEngine.Helpers