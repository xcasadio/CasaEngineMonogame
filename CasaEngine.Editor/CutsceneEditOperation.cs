using System;
using System.Collections.Generic;

namespace CasaEngine.Editor;

/// <summary>
/// One operation of the <c>--cutscene-edit-ops</c> automation option (ADR-0101, P14). The list is read as written: an operation
/// that is unknown or lacks an argument is kept as it is and refused when it is applied, without dropping the others.
/// </summary>
/// <param name="Text">The operation as written, without the surrounding blanks.</param>
/// <param name="Name">The name, up to the first colon, in lower case.</param>
/// <param name="Argument">What follows the first colon; for <c>move</c> and <c>draghover</c>, what lies between the first and the last colon. Null without a colon.</param>
/// <param name="Detail">For <c>move</c> and <c>draghover</c>, what follows the last colon (the index or the zone); null for every other operation, or without a second colon.</param>
public readonly record struct CutsceneEditOperation(string Text, string Name, string? Argument, string? Detail)
{
    /// <summary>Reads a list of operations separated by <c>;</c>. Empty items are skipped; a null or blank list gives no operation.</summary>
    public static CutsceneEditOperation[] ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<CutsceneEditOperation>();
        }

        var operations = new List<CutsceneEditOperation>();
        string[] parts = value.Split(';');
        for (int index = 0; index < parts.Length; index++)
        {
            string text = parts[index].Trim();
            if (text.Length > 0)
            {
                operations.Add(Parse(text));
            }
        }

        return operations.ToArray();
    }

    private static CutsceneEditOperation Parse(string text)
    {
        int firstColon = text.IndexOf(':');
        if (firstColon < 0)
        {
            return new CutsceneEditOperation(text, text.ToLowerInvariant(), null, null);
        }

        string name = text.Substring(0, firstColon).Trim().ToLowerInvariant();
        string rest = text.Substring(firstColon + 1).Trim();
        if (name is "move" or "draghover")
        {
            int lastColon = rest.LastIndexOf(':');
            if (lastColon >= 0)
            {
                return new CutsceneEditOperation(text, name, rest.Substring(0, lastColon).Trim(), rest.Substring(lastColon + 1).Trim());
            }
        }

        return new CutsceneEditOperation(text, name, rest, null);
    }
}
