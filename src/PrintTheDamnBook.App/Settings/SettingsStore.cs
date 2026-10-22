using System.IO;
using PrintTheDamnBook.Core.Layout;

namespace PrintTheDamnBook.App.Settings;

/// <summary>The settings file under %LOCALAPPDATA%. Loading and saving never throw; the rules are in Core.</summary>
public sealed class SettingsStore(string path)
{
    private const string AppFolder = "PrintTheDamnBook";
    private const string FileName = "settings.json";

    private readonly object _saveGate = new();

    public static SettingsStore AtDefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new SettingsStore(Path.Combine(root, AppFolder, FileName));
    }

    public PrintSettings Load()
    {
        return PrintSettingsJson.Load(path);
    }

    /// <summary>Writes the settings; false when the file could not be written. Saves run one at a time.</summary>
    public bool Save(PrintSettings settings)
    {
        lock (_saveGate)
        {
            return PrintSettingsJson.TrySave(path, settings);
        }
    }
}
