namespace PrintTheDamnBook.Core.Dino;

/// <summary>A book another app (Dino Reader) asked this one to open on the command line (Slice 19).</summary>
/// <param name="Path">The path as given, for the file name shown to the user.</param>
/// <param name="Language">The <c>--language</c> code asked for, or null; checked against the book's languages once it is read.</param>
/// <param name="IsUsable">True only for an existing local file ending in <c>.dino</c>.</param>
public sealed record HandoverRequest(string Path, string? Language, bool IsUsable)
{
    /// <summary>The file name alone, safe to show: the path's directories are never printed.</summary>
    public string FileName
    {
        get
        {
            var name = System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
            return name.Length > 0 ? name : Path;
        }
    }
}

/// <summary>Reads <c>PrintTheDamnBook.exe &lt;path-to.dino&gt; [--language &lt;code&gt;]</c>. The path is untrusted: unknown arguments are ignored.</summary>
public static class HandoverArguments
{
    public const string DinoExtension = ".dino";
    public const string LanguageFlag = "--language";

    private const string FlagPrefix = "--";

    /// <summary>The hand-over the arguments ask for (without the program name), or null when none names a path.</summary>
    public static HandoverRequest? Parse(IReadOnlyList<string> args, Func<string, bool> fileExists)
    {
        string? path = null;
        string? firstPositional = null;
        string? language = null;
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(LanguageFlag + "=", StringComparison.OrdinalIgnoreCase))
            {
                language = Clean(arg[(LanguageFlag.Length + 1)..]);
            }
            else if (arg.Equals(LanguageFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Count && IsLanguageValue(args[i + 1]))
                {
                    language = Clean(args[++i]);
                }
            }
            else if (!arg.StartsWith(FlagPrefix, StringComparison.Ordinal) && arg.Trim().Length > 0)
            {
                firstPositional ??= arg.Trim();
                if (path is null && arg.Trim().EndsWith(DinoExtension, StringComparison.OrdinalIgnoreCase))
                {
                    path = arg.Trim();
                }
            }
        }

        // The first .dino argument wins; with none, the first plain argument is kept only so the alert can name it.
        path ??= firstPositional;
        return path is null ? null : new HandoverRequest(path, language, IsUsablePath(path, fileExists));
    }

    /// <summary>A language value is not another flag and not the book's path.</summary>
    private static bool IsLanguageValue(string value)
    {
        return !value.StartsWith(FlagPrefix, StringComparison.Ordinal) && !value.Trim().EndsWith(DinoExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Clean(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }

    /// <summary>An absolute local path (drive letter, not a network share or device path) to an existing file named <c>*.dino</c>.</summary>
    public static bool IsUsablePath(string path, Func<string, bool> fileExists)
    {
        return path.EndsWith(DinoExtension, StringComparison.OrdinalIgnoreCase)
            && path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0
            && IsLocalDrivePath(path)
            && fileExists(path);
    }

    private static bool IsLocalDrivePath(string path)
    {
        return path.Length > 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/';
    }
}
