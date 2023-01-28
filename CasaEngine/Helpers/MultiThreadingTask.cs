
/*
Copyright (c) 2008-2013, Schneider, José Ignacio.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of Schneider, José Ignacio nor the names of its contributors may be used to endorse or promote products derived
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


using System.Collections.Generic;
using System.Threading;

namespace XNAFinalEngine.Helpers
{
    /// <summary>
    /// Creates several threads that perform the same task over different data.
    /// It also provides synchronization methods.
    /// T correspond to the parameter type.
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
    public class MultiThreadingTask<T>
    {


        // The task.
        private readonly Action<T> task;

        // This are the synchronization elements.
        private readonly ManualResetEvent[] taskDone, waitForWork;

        // The threads.
        private readonly List<Thread> threads;

        // Task parameters.
        private readonly T[] parameters;

        private readonly int[] processorAffinity;



        /// <summary>
        /// Obtain the number of logical cores available for multithreading.
        /// </summary>
        public MultiThreadingTask(Action<T> task, int numberOfThreads, int[] processorAffinity = null)
        {
            if (numberOfThreads <= 0)
                throw new ArgumentOutOfRangeException("numberOfThreads");
            this.task = task;
            taskDone = new ManualResetEvent[numberOfThreads];
            waitForWork = new ManualResetEvent[numberOfThreads];
            parameters = new T[numberOfThreads];
            threads = new List<Thread>(numberOfThreads);
            if (processorAffinity != null)
                this.processorAffinity = processorAffinity;
            else
                this.processorAffinity = ProcessorsInformation.ProcessorsAffinity;
            for (int i = 0; i < numberOfThreads; i++)
            {
                taskDone[i] = new ManualResetEvent(false);
                waitForWork[i] = new ManualResetEvent(false);
                threads.Add(new Thread(TaskManager));
                threads[i].Start(i);
            }
        } // MultiThreadingTask



        /// <summary>
        /// A thread could not be restarted. 
        /// The thread needs to sleep and wait for more work to perform.
        /// </summary>
        private void TaskManager(object parameter)
        {
            int index = (int)parameter;

            Thread.CurrentThread.IsBackground = true; // To destroy it when the application exits.
#if XBOX
                // http://msdn.microsoft.com/en-us/library/microsoft.xna.net_cf.system.threading.thread.setprocessoraffinity.aspx
                Thread.CurrentThread.SetProcessorAffinity(processorAffinity[index]);
#endif

            while (true)
            {
                waitForWork[index].WaitOne(); // Wait until a task is added.
                waitForWork[index].Reset();
                task.Invoke(parameters[index]);
                taskDone[index].Set(); // Indicates that that task was performed.
            }
        } // TaskManager



        /// <summary>
        ///  Start again the task.
        /// </summary>
        public void Start(int taskNumber, T parameter)
        {
            parameters[taskNumber] = parameter;
            waitForWork[taskNumber].Set();
        } // Start



        /// <summary>
        /// Call this if you need to wait for all tasks to be completed.
        /// </summary>
        public void WaitForTaskCompletition()
        {
            for (int i = 0; i < threads.Count; i++)
            {
                taskDone[i].WaitOne();
                taskDone[i].Reset();
            }
        } // WaitForTaskCompletition

        /// <summary>
        /// Call this if you need to wait for one task to be completed.
        /// </summary>
        public void WaitForTaskCompletition(int taskNumber)
        {
            taskDone[taskNumber].WaitOne();
            taskDone[taskNumber].Reset();
        } // WaitForTaskCompletition


    } // MultiThreadingTask
} // XNAFinalEngine.Helpers