using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App.Services;

/// <summary>Adds this exe to Windows' "Open with" list for <c>.dino</c> for the current user (HKCU only) on every start, writing only values that differ (Slice 19). A failure is logged and ignored.</summary>
public static class OpenWithRegistration
{
    private const int AssociationsChanged = 0x08000000;

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    /// <summary>Runs the registration on a worker thread so the window never waits for the registry.</summary>
    public static Task RegisterInBackground()
    {
        return Task.Run(Register);
    }

    private static void Register()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null || !string.Equals(Path.GetFileName(exe), OpenWithRegistrar.ExeName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[handover] open-with: skipped, not running as PrintTheDamnBook.exe");
                return;
            }

            if (!OpenWithRegistrar.IsRegistrableLocation(exe))
            {
                Debug.WriteLine("[handover] open-with: skipped, a build output or packaged install");
                return;
            }

            var written = OpenWithRegistrar.Apply(new CurrentUserStore(), exe);
            Debug.WriteLine($"[handover] open-with: {written} value(s) written");
            if (written > 0)
            {
                SHChangeNotify(AssociationsChanged, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Debug.WriteLine($"[handover] open-with: failed and ignored: {ex.Message}");
        }
    }

    /// <summary>The real current-user hive; reads without creating keys.</summary>
    private sealed class CurrentUserStore : IOpenWithStore
    {
        public string? Get(string key, string? name)
        {
            using var opened = Registry.CurrentUser.OpenSubKey(key);
            if (opened is null || (name is not null && !opened.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)))
            {
                return null;
            }

            return opened.GetValue(name ?? string.Empty) switch
            {
                string text => text,
                null => null,
                _ => string.Empty,
            };
        }

        public void Set(string key, string? name, string value)
        {
            using var created = Registry.CurrentUser.CreateSubKey(key);
            created.SetValue(name ?? string.Empty, value, RegistryValueKind.String);
        }
    }
}
