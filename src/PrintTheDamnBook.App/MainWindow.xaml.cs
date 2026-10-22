using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrintTheDamnBook.App.Printing;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.Settings;
using PrintTheDamnBook.App.ViewModels;
using PrintTheDamnBook.App.Views;

namespace PrintTheDamnBook.App;

public partial class MainWindow : Window
{
    private const string ImageFilter = "Page images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

    public MainWindow()
    {
        InitializeComponent();
        var print = new PrintViewModel(new PrinterService(), new NocturnePrompts(), SettingsStore.AtDefaultPath());
        DataContext = new MainViewModel(PickFolder, print);
        Closing += (_, _) => print.FlushSettings();
        Loaded += async (_, _) => await print.LoadPrintersAsync();
        SourceInitialized += (_, _) => new SnapLayoutHook(this, MaximizeButton).Attach();
        StateChanged += (_, _) => ShowMaximizeGlyph();
    }

    /// <summary>The folder under the cursor, looked up once per drag instead of on every DragOver.</summary>
    private string? _dragFolder;

    private bool _dragChecked;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the folder with the page images" };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private async void OnAddPagesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Add pages", Multiselect = true, Filter = ImageFilter };
        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.AddPagesAsync(dialog.FileNames);
        }
    }

    private void OnLicencesClick(object sender, RoutedEventArgs e)
    {
        new NocturneDialog(this, "Licences", FontLicences.ReadAll(), "Close", null, wide: true).ShowDialog();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    internal void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ShowMaximizeGlyph()
    {
        Ui.SetIcon(MaximizeButton, (string)FindResource(WindowState == WindowState.Maximized ? "IconCopy" : "IconSquare"));
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (!_dragChecked)
        {
            _dragFolder = DroppedFolder(e);
            _dragChecked = true;
        }

        e.Effects = _dragFolder is not null && CanDropFolder() ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        _dragChecked = false;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        _dragChecked = false;
        if (DroppedFolder(e) is { } folder && CanDropFolder())
        {
            await ViewModel.LoadFolderAsync(folder);
        }

        e.Handled = true;
    }

    /// <summary>A dropped folder replaces the book, so it is only taken on the Open step.</summary>
    private bool CanDropFolder() => ViewModel.IsOpenStep && !ViewModel.IsBusy;

    private static string? DroppedFolder(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths.FirstOrDefault(Directory.Exists)
            : null;
    }
}
