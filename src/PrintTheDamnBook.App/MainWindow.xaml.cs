using System.Windows;
using Microsoft.Win32;
using PrintTheDamnBook.App.ViewModels;

namespace PrintTheDamnBook.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(PickFolder);
    }

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the folder with the page images" };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }
}
