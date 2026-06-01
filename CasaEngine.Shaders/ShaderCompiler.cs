using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CasaEngine.Shaders;

// source copy and modified from MonoGame
// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'Licences\MonoGame.txt', which is part of this source code package.
public static class ShaderCompiler
{
    private const string mgfxcPath = "mgfxc\\mgfxc.dll";
    private static readonly Regex DiagnosticRegex = new(
        @"^(?<file>.*?)?\((?<location>\d+(?:,\d+(?:-\d+)?)?)\)\s*:\s*(?<message>.+)$",
        RegexOptions.Compiled);

    public static ShaderCompiled Compile(string sourceFile, string defines, TargetPlatform platform, EffectProcessorDebugMode debugMode = EffectProcessorDebugMode.Optimize)
    {
        ShaderCompiled shaderCompiled = new ShaderCompiled();
        var mgfxc = ResolveMgfxcPath();
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

        var success = ProcessLauncher.Run("dotnet", arguments, out var stdout, out var stderr) == 0;
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

        shaderCompiled.Logs = ProcessErrorsAndWarnings(!success, stderr, sourceFile);

        return shaderCompiled;
    }

    private static string ResolveMgfxcPath()
    {
        string appBaseDirectoryPath = Path.Combine(AppContext.BaseDirectory, mgfxcPath);
        if (File.Exists(appBaseDirectoryPath))
        {
            return appBaseDirectoryPath;
        }

        string currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, mgfxcPath);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        return appBaseDirectoryPath;
    }

    private static string GetProfileForPlatform(TargetPlatform platform)
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

    internal static string ProcessErrorsAndWarnings(bool buildFailed, string shaderErrorsAndWarnings, string sourceFile)
    {
        string diagnostics = FormatCompilerDiagnostics(shaderErrorsAndWarnings, sourceFile);

        if (buildFailed)
        {
            throw new InvalidOperationException($"Compile shader {sourceFile}: {diagnostics}");
        }

        return diagnostics;
    }

    internal static string FormatCompilerDiagnostics(string shaderErrorsAndWarnings, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(shaderErrorsAndWarnings))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var diagnosticLines = shaderErrorsAndWarnings.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < diagnosticLines.Length; i++)
        {
            string formattedLine = FormatCompilerDiagnosticLine(diagnosticLines[i], sourceFile);
            if (string.IsNullOrWhiteSpace(formattedLine))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(formattedLine);
        }

        return builder.ToString();
    }

    internal static string FormatCompilerDiagnosticLine(string rawDiagnosticLine, string sourceFile)
    {
        string diagnosticLine = rawDiagnosticLine.Trim();
        if (diagnosticLine.Length == 0)
        {
            return string.Empty;
        }

        Match match = DiagnosticRegex.Match(diagnosticLine);
        if (!match.Success)
        {
            return diagnosticLine;
        }

        string fileName = ResolveDiagnosticFileName(match.Groups["file"].Value, sourceFile);
        string lineAndColumn = match.Groups["location"].Value;
        string message = match.Groups["message"].Value.Trim();
        return $"{fileName}({lineAndColumn}): {message}";
    }

    private static string ResolveDiagnosticFileName(string fileName, string sourceFile)
    {
        string trimmedFileName = fileName.Trim();
        if (string.IsNullOrEmpty(trimmedFileName))
        {
            return sourceFile;
        }

        if (Path.IsPathRooted(trimmedFileName))
        {
            return trimmedFileName;
        }

        string? sourceDirectory = Path.GetDirectoryName(sourceFile);
        if (string.IsNullOrEmpty(sourceDirectory))
        {
            return trimmedFileName;
        }

        return Path.GetFullPath(Path.Combine(sourceDirectory, trimmedFileName));
    }
}