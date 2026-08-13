using System.Diagnostics;
using System.Text;
using CasaEngine.Core.Logging;

namespace CasaEngine.EditorServices.Scripting;

/// <summary>
/// Builds the gameplay scripts csproj out of process with `dotnet build`. Every build
/// goes to its own timestamped directory so a loaded dll never blocks the next build,
/// and MSBuild diagnostics are surfaced in the editor logs.
/// </summary>
public static class EditorScriptBuildService
{
    public static ScriptBuildResult Build(
        string csprojPath,
        string outputRootDirectory,
        string? expectedAssemblyFileName = null,
        int timeoutMilliseconds = 180000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csprojPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootDirectory);

        string fullCsprojPath = Path.GetFullPath(csprojPath);
        string buildDirectory = Path.Combine(
            Path.GetFullPath(outputRootDirectory),
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));

        if (!File.Exists(fullCsprojPath))
        {
            string error = $"Gameplay csproj not found: {fullCsprojPath}";
            Logs.WriteError(error);
            return new ScriptBuildResult { Success = false, BuildDirectory = buildDirectory, Errors = new[] { error } };
        }

        Directory.CreateDirectory(buildDirectory);
        Logs.WriteInfo($"Building gameplay scripts: {fullCsprojPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{fullCsprojPath}\" -c Debug -o \"{buildDirectory}\" --nologo -v q",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(fullCsprojPath)!,
        };

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Logs.WriteWarning($"Could not kill the timed-out build process: {ex.Message}");
            }

            string error = $"Gameplay scripts build timed out after {timeoutMilliseconds / 1000}s.";
            Logs.WriteError(error);
            return new ScriptBuildResult { Success = false, BuildDirectory = buildDirectory, Errors = new[] { error } };
        }

        // Flush the async readers.
        process.WaitForExit();

        var errors = ExtractErrors(output.ToString());

        if (process.ExitCode != 0 || errors.Count > 0)
        {
            if (errors.Count == 0)
            {
                errors.Add($"dotnet build exited with code {process.ExitCode}.");
            }

            foreach (var error in errors)
            {
                Logs.WriteError($"[Scripts] {error}");
            }

            return new ScriptBuildResult { Success = false, BuildDirectory = buildDirectory, Errors = errors };
        }

        string assemblyFileName = expectedAssemblyFileName
            ?? Path.GetFileNameWithoutExtension(fullCsprojPath) + ".dll";
        string assemblyPath = Path.Combine(buildDirectory, assemblyFileName);

        if (!File.Exists(assemblyPath))
        {
            string error = $"Build succeeded but the expected assembly is missing: {assemblyPath}";
            Logs.WriteError(error);
            return new ScriptBuildResult { Success = false, BuildDirectory = buildDirectory, Errors = new[] { error } };
        }

        Logs.WriteInfo($"Gameplay scripts built: {assemblyPath}");
        return new ScriptBuildResult
        {
            Success = true,
            BuildDirectory = buildDirectory,
            OutputAssemblyPath = assemblyPath,
        };
    }

    private static List<string> ExtractErrors(string buildOutput)
    {
        var errors = new List<string>();
        foreach (var rawLine in buildOutput.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Contains(": error ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("error ", StringComparison.OrdinalIgnoreCase))
            {
                if (!errors.Contains(line))
                {
                    errors.Add(line);
                }
            }
        }

        return errors;
    }
}
