using System.IO;
using System.Windows;

namespace PrintTheDamnBook.App.Services;

/// <summary>The licence texts of the bundled fonts, read from the exe's own resources.</summary>
public static class FontLicences
{
    private const string FontsFolder = "/Fonts/";
    private const string Separator = "\n\n----------------------------------------\n\n";

    // Andika-Bold, Andika-Italic and Vazirmatn-Bold (Slice 46, KDP styled text) are the same families, so their licences are the Andika and Vazirmatn texts below.
    private static readonly string[] Files = ["Inter-LICENSE.txt", "Phosphor-LICENSE.txt", "Andika-LICENSE.txt", "Vazirmatn-LICENSE.txt"];

    public static string ReadAll()
    {
        return string.Join(Separator, Files.Select(Read));
    }

    private static string Read(string file)
    {
        var info = Application.GetResourceStream(new Uri(FontsFolder + file, UriKind.Relative));
        using var reader = new StreamReader(info.Stream);
        return reader.ReadToEnd().Trim();
    }
}
