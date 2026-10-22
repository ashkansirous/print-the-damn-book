using System.IO;

namespace PrintTheDamnBook.App.Services;

/// <summary>Lists the page images (jpg, jpeg, png) in a folder.</summary>
public static class FolderImages
{
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];

    public static IReadOnlyList<string> List(string folder) =>
        Directory.EnumerateFiles(folder)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
}
