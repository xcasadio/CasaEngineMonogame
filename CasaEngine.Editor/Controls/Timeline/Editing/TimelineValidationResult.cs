#nullable enable

namespace CasaEngine.Editor.Controls.Timeline.Editing;

public sealed class TimelineValidationResult
{
    public bool IsValid { get; init; }

    public string? Message { get; init; }

    public static TimelineValidationResult Valid { get; } = new()
    {
        IsValid = true,
    };

    public static TimelineValidationResult Error(string message)
    {
        return new TimelineValidationResult
        {
            IsValid = false,
            Message = message,
        };
    }
}
