using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CasaEngine.Shaders;

// source copy and modified from MonoGame
// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'Licences\MonoGame.txt', which is part of this source code package.
public static class ProcessLauncher
{
    public static int Run(string command, string arguments)
    {
        string stdout, stderr;
        var result = Run(command, arguments, out stdout, out stderr);
        if (result < 0)
            throw new Exception(string.Format("{0} returned exit code {1}", command, result));

        return result;
    }

    public static int Run(string command, string arguments, out string stdout, out string stderr, string stdin = null)
    {
        // This particular case is likely to be the most common and thus
        // warrants its own specific error message rather than falling
        // back to a general exception from Process.Start()
        //var fullPath = FindCommand(command);
        //if (string.IsNullOrEmpty(fullPath))
        //    throw new Exception(string.Format("Couldn't locate external tool '{0}'.", command));

        // We can't reference ref or out parameters from within
        // lambdas (for the thread functions), so we have to store
        // the data in a temporary variable and then assign these
        // variables to the out parameters.
        var stdoutTemp = string.Empty;
        var stderrTemp = string.Empty;

        var processInfo = new ProcessStartInfo
        {
            Arguments = arguments,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            ErrorDialog = false,
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        using var process = new Process();
        process.StartInfo = processInfo;

        process.Start();

        // We have to run these in threads, because using ReadToEnd
        // on one stream can deadlock if the other stream's buffer is
        // full.
        var stdoutThread = new Thread(() =>
        {
            var memory = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(memory);
            var bytes = new byte[memory.Position];
            memory.Seek(0, SeekOrigin.Begin);
            memory.Read(bytes, 0, bytes.Length);
            stdoutTemp = System.Text.Encoding.ASCII.GetString(bytes);
        });
        var stderrThread = new Thread(() =>
        {
            var memory = new MemoryStream();
            process.StandardError.BaseStream.CopyTo(memory);
            var bytes = new byte[memory.Position];
            memory.Seek(0, SeekOrigin.Begin);
            memory.Read(bytes, 0, bytes.Length);
            stderrTemp = System.Text.Encoding.ASCII.GetString(bytes);
        });

        stdoutThread.Start();
        stderrThread.Start();

        if (stdin != null)
        {
            process.StandardInput.Write(System.Text.Encoding.ASCII.GetBytes(stdin));
        }

        // Make sure interactive prompts don't block.
        process.StandardInput.Close();

        process.WaitForExit();

        stdoutThread.Join();
        stderrThread.Join();

        stdout = stdoutTemp;
        stderr = stderrTemp;

        return process.ExitCode;
    }
}