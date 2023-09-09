using System;
using System.IO;
using System.Text.RegularExpressions;
using CasaEngine.Core.Logger;

namespace EditorWpf.Shaders;

// source copy and modified from MonoGame
// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'Licences\MonoGame.txt', which is part of this source code package.
public class ShaderCompiler
{
    private const string mgfxcPath = "mgfxc\\mgfxc.dll";

    public ShaderCompiled Compile(string sourceFile, string defines, TargetPlatform platform, EffectProcessorDebugMode debugMode = EffectProcessorDebugMode.Optimize)
    {
        ShaderCompiled shaderCompiled = new ShaderCompiled();
        var mgfxc = Path.Combine(Environment.CurrentDirectory, mgfxcPath);
        var destFile = Path.GetTempFileName();
        var arguments = "\"" + mgfxc + "\" \"" + sourceFile + "\" \"" + destFile + "\" /Profile:" + GetProfileForPlatform(platform);

        if (debugMode == EffectProcessorDebugMode.Debug)
        {
            arguments += " /Debug";
        }

        if (!string.IsNullOrWhiteSpace(defines))
        {
            arguments += " \"/Defines:" + defines + "\"";
        }

        string stdout, stderr;

        var success = ProcessLauncher.Run("dotnet", arguments, out stdout, out stderr) == 0;
        shaderCompiled.ByteCode = success ? File.ReadAllBytes(destFile) : null;

        File.Delete(destFile);

        var stdOutLines = stdout.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in stdOutLines)
        {
            if (line.StartsWith("Dependency:") && line.Length > 12)
            {
                shaderCompiled.AddDependency(line.Substring(12));
            }
        }

        ProcessErrorsAndWarnings(!success, stderr, sourceFile);

        return shaderCompiled;
    }

    private string GetProfileForPlatform(TargetPlatform platform)
    {
        switch (platform)
        {
            case TargetPlatform.Windows:
            case TargetPlatform.WindowsPhone8:
            case TargetPlatform.WindowsStoreApp:
                return "DirectX_11";
            case TargetPlatform.iOS:
            case TargetPlatform.Android:
            case TargetPlatform.DesktopGL:
            case TargetPlatform.MacOSX:
            case TargetPlatform.RaspberryPi:
            case TargetPlatform.Web:
                return "OpenGL";
        }

        return platform.ToString();
    }

    private static void ProcessErrorsAndWarnings(bool buildFailed, string shaderErrorsAndWarnings, string sourceFile)
    {
        // Split the errors and warnings into individual lines.
        var errorsAndWarningArray = shaderErrorsAndWarnings.Split(new[] { "\n", "\r", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        var logs = string.Empty;
        var errorOrWarning = new Regex(@"(.*)\(([0-9]*(,[0-9]+(-[0-9]+)?)?)\)\s*:\s*(.*)", RegexOptions.Compiled);
        var allErrorsAndWarnings = string.Empty;

        // Process all the lines.
        for (var i = 0; i < errorsAndWarningArray.Length; i++)
        {
            var match = errorOrWarning.Match(errorsAndWarningArray[i]);
            if (!match.Success || match.Groups.Count != 4)
            {
                // Just log anything we don't recognize as a warning.
                if (buildFailed)
                {
                    allErrorsAndWarnings += errorsAndWarningArray[i] + Environment.NewLine;
                }
                else
                {
                    logs += errorsAndWarningArray[i];
                }

                continue;
            }

            var fileName = match.Groups[1].Value;
            var lineAndColumn = match.Groups[2].Value;
            var message = match.Groups[3].Value;

            // Try to ensure a good file name for the error message.
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = sourceFile;
            }
            else if (!File.Exists(fileName))
            {
                var folder = Path.GetDirectoryName(sourceFile);
                fileName = Path.Combine(folder, fileName);
            }

            // If we got an exception then we'll be throwing an exception 
            // below, so just gather the lines to throw later.
            if (buildFailed)
            {
                allErrorsAndWarnings += $"{fileName}({lineAndColumn}):" + errorsAndWarningArray[i] + Environment.NewLine;
            }
            else
            {
                logs += $"{fileName}({lineAndColumn}):" + message;
            }
        }

        LogManager.Instance.WriteLineInfo(logs);

        if (buildFailed)
        {
            throw new InvalidOperationException($"Compile shader {sourceFile}: {allErrorsAndWarnings}");
        }
    }
}