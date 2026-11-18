namespace PrintTheDamnBook.Core.Text;

/// <summary>The one cleaning of story words: what is compared to tell whether a page's words were changed, and what Slice 23 prints.</summary>
public static class StoryLines
{
    private const char LineBreak = '\n';

    /// <summary>
    /// <paramref name="text"/> with <c>\r\n</c> and <c>\r</c> read as <c>\n</c>, each line trimmed at both ends, and empty or white-space-only
    /// lines dropped; single line breaks stay. A blank line between paragraphs therefore equals none.
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', LineBreak).Split(LineBreak)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        return string.Join(LineBreak, lines);
    }

    /// <summary>True when both read the same once cleaned: only blank lines, line-end spaces or line-break style differ.</summary>
    public static bool SameWords(string? a, string? b) => string.Equals(Clean(a), Clean(b), StringComparison.Ordinal);
}
