using System.Reflection;
using QuestPDF.Drawing;

namespace PrintTheDamnBook.Kdp;

/// <summary>The story fonts, registered with QuestPDF from the copies embedded in this assembly (the App's own <c>.ttf</c> files, linked, not copied).</summary>
internal static class KdpFonts
{
    private static readonly string[] Resources = ["Fonts.Andika-Regular.ttf", "Fonts.Vazirmatn-Regular.ttf"];
    private static readonly Lock Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in Resources)
            {
                using var stream = assembly.GetManifestResourceStream(name) ?? throw new KdpException($"The font {name} is missing from the program.");
                FontManager.RegisterFontFromStream(stream);
            }

            _registered = true;
        }
    }
}
