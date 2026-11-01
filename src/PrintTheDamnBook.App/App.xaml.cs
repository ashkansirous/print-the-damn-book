using System.Windows;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.Core.Dino;

namespace PrintTheDamnBook.App;

public partial class App : Application
{
    /// <summary>The book another app asked this one to open on the command line (Slice 19); null for a plain start.</summary>
    public static HandoverRequest? Handover { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Handover = LaunchArguments.Read(e.Args);
        _ = OpenWithRegistration.RegisterInBackground();
        base.OnStartup(e);
    }
}
