namespace PrintTheDamnBook.Core.Text;

/// <summary>The bundled story fonts and which one a text is set in: Vazirmatn when it has Arabic-script letters, otherwise Andika (which has no Arabic glyphs).</summary>
public static class StoryFont
{
    public const string Andika = "Andika";
    public const string Vazirmatn = "Vazirmatn";

    public static string For(string? text)
    {
        return TextDirection.HasArabicScriptLetter(text) ? Vazirmatn : Andika;
    }
}
