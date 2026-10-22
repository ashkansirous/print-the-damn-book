using System.Windows;
using Microsoft.Win32;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.App.ViewModels;

namespace PrintTheDamnBook.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var print = new PrintViewModel(new PrinterService(), new MessageBoxPrompts(), SettingsStore.AtDefaultPath());
        DataContext = new MainViewModel(PickFolder, print);
        Closing += (_, _) => print.FlushSettings();
        Loaded += async (_, _) => await print.LoadPrintersAsync();
    }

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the folder with the page images" };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
}
