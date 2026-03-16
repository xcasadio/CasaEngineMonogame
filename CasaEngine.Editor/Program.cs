using System;
using System.IO;
using System.Text;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var automationOptions = CasaEngine.Editor.EditorAutomationOptions.Parse(args);
        try
        {
            using var game = new CasaEngine.Editor.Game1(automationOptions);
            game.Run();
        }
        catch (Exception ex)
        {
            string outputPath = !string.IsNullOrWhiteSpace(automationOptions.DiagnosticsOutputPath)
                ? Path.GetFullPath(automationOptions.DiagnosticsOutputPath)
                : Path.Combine(Environment.CurrentDirectory, "editor-automation-error.txt");

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var builder = new StringBuilder();
            builder.AppendLine("CasaEngine Editor automation failure");
            builder.AppendLine($"Captured at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            builder.AppendLine();
            builder.AppendLine(ex.ToString());
            File.WriteAllText(outputPath, builder.ToString());
            throw;
        }
    }
}