namespace PrintTheDamnBook.Core.Dino;

/// <summary>One value to keep in the current user's registry; a null <see cref="Name"/> is the key's default value.</summary>
public sealed record OpenWithValue(string Key, string? Name, string Value);

/// <summary>Where the plan is read and written: the real current-user hive in the app, a dictionary in tests.</summary>
public interface IOpenWithStore
{
    string? Get(string key, string? name);

    void Set(string key, string? name, string value);
}

/// <summary>
/// Puts this app on Windows' "Open with" list for <c>.dino</c>, for the current user only (Slice 19). Only the keys below are written; the
/// <c>UserChoice</c> key and the default handler are never touched. Keys per Microsoft's "Application Registration" and "How to Include an
/// Application on the Open with Dialog Box".
/// </summary>
public static class OpenWithRegistrar
{
    public const string ProgId = "PrintTheDamnBook.dino";
    public const string ExeName = "PrintTheDamnBook.exe";
    public const string FriendlyName = "Dino Reader book";

    private const string Classes = @"Software\Classes\";

    private static readonly string[] SkippedPaths = [@"\bin\debug\", @"\bin\release\", @"\obj\", @"\windowsapps\"];

    /// <summary>
    /// True for an install worth pointing "Open with" at: not a build output (<c>\bin\Debug\</c>, <c>\bin\Release\</c>, <c>\obj\</c>) and not a
    /// packaged app (<c>\WindowsApps\</c>, where a registry write is virtualised and the command would not launch). A folder that is merely
    /// named <c>bin</c> is fine.
    /// </summary>
    public static bool IsRegistrableLocation(string exePath)
    {
        var path = exePath.Replace('/', '\\');
        return !SkippedPaths.Any(skipped => path.Contains(skipped, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every value to hold for an app at <paramref name="exePath"/>.</summary>
    public static IReadOnlyList<OpenWithValue> Plan(string exePath)
    {
        var command = $"\"{exePath}\" \"%1\"";
        var progId = Classes + ProgId;
        var app = Classes + @"Applications\" + ExeName;
        return
        [
            new(Classes + @".dino\OpenWithProgids", ProgId, string.Empty),
            new(progId, null, FriendlyName),
            new(progId + @"\DefaultIcon", null, $"\"{exePath}\",0"),
            new(progId + @"\shell\open\command", null, command),
            new(app + @"\shell\open\command", null, command),
            new(app + @"\SupportedTypes", HandoverArguments.DinoExtension, string.Empty),
        ];
    }

    /// <summary>Writes the values that differ (so a moved exe gets its command rewritten and a second run writes nothing); returns how many it wrote.</summary>
    public static int Apply(IOpenWithStore store, string exePath)
    {
        var written = 0;
        foreach (var value in Plan(exePath))
        {
            if (store.Get(value.Key, value.Name) != value.Value)
            {
                store.Set(value.Key, value.Name, value.Value);
                written++;
            }
        }

        return written;
    }
}
