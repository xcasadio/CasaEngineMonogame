namespace CasaEngine.Framework.Cutscenes;

public sealed record CutsceneValidationMessage(CutsceneValidationSeverity Severity, string Path, string Message);