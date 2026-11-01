using System.Globalization;
using System.Text.RegularExpressions;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>
/// The pure parts of the Dino cache (Slice 19): the <c>.complete</c> manifest (one <c>size TAB name</c> line per picture, written last, so a
/// folder that has one and still matches it is finished and never changes) and which folder names the clean-up may remove.
/// </summary>
public static class DinoCacheLayout
{
    public const string ManifestFile = ".complete";
    public const int HashLength = 16;

    private const char Separator = '\t';

    private static readonly Regex LeftoverName = new(@"\.(tmp|old|alt)-[0-9a-f]{32}\z", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string[] FormatManifest(IEnumerable<(string Name, long Size)> files)
    {
        return files.Select(f => f.Size.ToString(CultureInfo.InvariantCulture) + Separator + f.Name).ToArray();
    }

    /// <summary>The names and sizes a manifest lists; null when a line is malformed or a name repeats.</summary>
    public static Dictionary<string, long>? ParseManifest(IEnumerable<string> lines)
    {
        var listed = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Where(l => l.Length > 0))
        {
            var parts = line.Split(Separator, 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || !listed.TryAdd(parts[1], size))
            {
                return null;
            }
        }

        return listed;
    }

    /// <summary>True when the manifest names exactly <paramref name="expected"/> and each file is still the size it was written with.</summary>
    public static bool Matches(IReadOnlyDictionary<string, long>? listed, IReadOnlyCollection<string> expected, Func<string, long?> sizeOf)
    {
        return listed is not null && listed.Count == expected.Count && expected.All(n => listed.TryGetValue(n, out var size) && sizeOf(n) == size);
    }

    /// <summary>True when <paramref name="folder"/> has a manifest naming exactly <paramref name="expected"/> and every file still has its size.</summary>
    public static bool Verify(string folder, IReadOnlyCollection<string> expected)
    {
        try
        {
            var listed = ParseManifest(File.ReadAllLines(Path.Combine(folder, ManifestFile)));
            return Matches(listed, expected, n => new FileInfo(Path.Combine(folder, n)) is { Exists: true } info ? info.Length : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Writes the manifest of the files now in <paramref name="folder"/>; call it after the last file is written.</summary>
    public static void WriteManifest(string folder, IReadOnlyCollection<string> names)
    {
        var files = names.Select(n => (n, new FileInfo(Path.Combine(folder, n)).Length));
        File.WriteAllLines(Path.Combine(folder, ManifestFile), FormatManifest(files));
    }

    /// <summary>True for a leftover of an open that died: <c>&lt;folder&gt;.tmp-&lt;guid&gt;</c> , <c>&lt;folder&gt;.old-&lt;guid&gt;</c> or <c>&lt;folder&gt;.alt-&lt;guid&gt;</c>.</summary>
    public static bool IsLeftover(string folderName) => LeftoverName.IsMatch(folderName);

    /// <summary>True for <c>&lt;id&gt;-&lt;hash&gt;</c> of this book that is not <paramref name="currentName"/> (an older version of it); never another book's.</summary>
    public static bool IsOlderVersion(string folderName, string bookId, string currentName)
    {
        var older = new Regex(@"\A" + Regex.Escape(bookId) + "-[0-9a-f]{" + HashLength + @"}\z", RegexOptions.IgnoreCase);
        return older.IsMatch(folderName) && !string.Equals(folderName, currentName, StringComparison.OrdinalIgnoreCase);
    }
}
