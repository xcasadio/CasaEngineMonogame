using System.Text;

namespace CasaEngine.Framework.Dialogue.Yarn;

/// <summary>
/// Splits the raw text of a Yarn <c>&lt;&lt;command&gt;&gt;</c> line into a command name and its
/// arguments, the same way the Yarn Spinner Unity integration does: whitespace-separated tokens,
/// with double-quoted segments kept together as a single argument (quotes removed).
/// </summary>
internal static class YarnCommandLine
{
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return tokens;
        }

        int index = 0;
        int length = text.Length;
        var token = new StringBuilder();
        while (index < length)
        {
            while (index < length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= length)
            {
                break;
            }

            token.Clear();
            if (text[index] == '"')
            {
                index++;
                while (index < length && text[index] != '"')
                {
                    token.Append(text[index]);
                    index++;
                }

                if (index < length)
                {
                    index++;
                }
            }
            else
            {
                while (index < length && !char.IsWhiteSpace(text[index]))
                {
                    token.Append(text[index]);
                    index++;
                }
            }

            tokens.Add(token.ToString());
        }

        return tokens;
    }
}
