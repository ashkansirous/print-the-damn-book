using System.IO.Compression;
using System.Text;
using PrintTheDamnBook.Kdp;
using SkiaSharp;

namespace PrintTheDamnBook.Kdp.Tests;

/// <summary>A small book made in code: pictures drawn here (no user art is committed) and short words.</summary>
internal static class SyntheticBook
{
    public const int SourceWidth = 1792;
    public const int SourceHeight = 2400;

    public static byte[] Picture(int index, int width = SourceWidth, int height = SourceHeight)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = new SKColor((byte)(40 + index * 60), 120, 200) };
        canvas.Clear(SKColors.White);
        canvas.DrawRect(0, 0, width, height / 2, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    public static KdpBookInput Input(int pages, string language = "en", string? longText = null)
    {
        var texts = Enumerable.Range(1, pages).Select(i => longText ?? $"Page {i}: Pip was in a hurry. \"Now I can play,\" said Pip.").ToList();
        var pictures = Enumerable.Range(0, pages).Select(i => Picture(i)).ToList();
        return new KdpBookInput("Freddy and the Test", language, null, 2026, texts, pictures);
    }

    /// <param name="pages">How many story pages the book lists.</param>
    /// <param name="withoutPicture">How many of the last pages have no picture file in the archive (the reader skips them).</param>
    public static string WriteDino(string path, int pages, int withoutPicture = 0)
    {
        var story = new StringBuilder("{\"Title\":\"Freddy and the Test\",\"Language\":\"en\",\"Pages\":[");
        story.AppendJoin(',', Enumerable.Range(1, pages).Select(i => $"{{\"Text\":\"Page {i} of the test.\",\"ImageFile\":\"page-{i}.jpg\"}}"));
        story.Append("]}");
        using var file = File.Create(path);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        Add(zip, "package.json", Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"id\":\"test1\",\"title\":\"Freddy and the Test\",\"language\":\"en\"}"));
        Add(zip, "story.json", Encoding.UTF8.GetBytes(story.ToString()));
        for (var i = 1; i <= pages - withoutPicture; i++)
        {
            Add(zip, $"page-{i}.jpg", Picture(i));
        }

        return path;
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(bytes);
    }

    public static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ptdb-kdp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
