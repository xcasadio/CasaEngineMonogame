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

namespace CasaEngine.Core.MultiThreading;

public class MultiThreadingTask<T>
{
    private readonly Action<T> _task;
    private readonly ManualResetEvent[] _taskDone, _waitForWork;
    private readonly List<Thread> _threads;
    private readonly T[] _parameters;

    public MultiThreadingTask(Action<T> task, int numberOfThreads)
    {
        if (numberOfThreads <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfThreads));
        }

        _task = task;
        _taskDone = new ManualResetEvent[numberOfThreads];
        _waitForWork = new ManualResetEvent[numberOfThreads];
        _parameters = new T[numberOfThreads];
        _threads = new List<Thread>(numberOfThreads);

        for (var i = 0; i < numberOfThreads; i++)
        {
            _taskDone[i] = new ManualResetEvent(false);
            _waitForWork[i] = new ManualResetEvent(false);
            _threads.Add(new Thread(TaskManager));
            _threads[i].Start(i);
        }
    }

    private void TaskManager(object parameter)
    {
        var index = (int)parameter;

        Thread.CurrentThread.IsBackground = true; // To destroy it when the application exits.

        while (true)
        {
            _waitForWork[index].WaitOne(); // Wait until a task is added.
            _waitForWork[index].Reset();
            _task.Invoke(_parameters[index]);
            _taskDone[index].Set(); // Indicates that that task was performed.
        }
    }

    public void Start(int taskNumber, T parameter)
    {
        _parameters[taskNumber] = parameter;
        _waitForWork[taskNumber].Set();
    }

    public void WaitForTaskCompletition()
    {
        for (var i = 0; i < _threads.Count; i++)
        {
            _taskDone[i].WaitOne();
            _taskDone[i].Reset();
        }
    }

    public void WaitForTaskCompletition(int taskNumber)
    {
        _taskDone[taskNumber].WaitOne();
        _taskDone[taskNumber].Reset();
    }
}