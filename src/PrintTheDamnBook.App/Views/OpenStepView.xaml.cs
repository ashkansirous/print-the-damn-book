using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.ViewModels;

namespace PrintTheDamnBook.App.Views;

public partial class OpenStepView : UserControl
{
    public OpenStepView()
    {
        InitializeComponent();
    }

    private bool _storeOpen;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>Takes a dragged .dino file; anything else (a folder) is left for the window to handle.</summary>
    private void OnDinoDragOver(object sender, DragEventArgs e)
    {
        if (DroppedDino(e) is not null)
        {
            e.Effects = ViewModel is { IsBusy: false } ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
    }

    private async void OnDinoDrop(object sender, DragEventArgs e)
    {
        if (DroppedDino(e) is not { } path)
        {
            return;
        }

        e.Handled = true;
        if (ViewModel is { IsBusy: false } viewModel)
        {
            await viewModel.LoadDinoAsync(path);
        }
    }

    /// <summary>Opens the store dialog; a book it downloaded goes through the same open as a picked <c>.dino</c> file.</summary>
    private async void OnGetStoryClick(object sender, RoutedEventArgs e)
    {
        if (_storeOpen || ViewModel is not { IsBusy: false } viewModel || Window.GetWindow(this) is not { } owner)
        {
            return;
        }

        _storeOpen = true;
        var button = (Button)sender;
        var enabledBinding = BindingOperations.GetBindingBase(button, IsEnabledProperty);
        button.IsEnabled = false;
        try
        {
            await RunStoreAsync(viewModel, owner);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            viewModel.ReportOpenFailure(ex);
        }
        finally
        {
            if (enabledBinding is not null)
            {
                button.SetBinding(IsEnabledProperty, enabledBinding);
            }
            else
            {
                button.ClearValue(IsEnabledProperty);
            }
            _storeOpen = false;
        }
    }

    private static async Task RunStoreAsync(MainViewModel viewModel, Window owner)
    {
        using var client = new DinoStoreClient();
        await Task.Run(client.SweepStaleParts);
        var store = new DinoStoreViewModel(client);
        var dialog = new DinoStoreDialog(owner, store);
        dialog.ShowDialog();
        await store.StopAsync();
        if (dialog.DownloadedPath is { } path)
        {
            var result = await viewModel.LoadDinoAsync(path);
            client.Settle(path, result);
        }
    }

    private static string? DroppedDino(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths.FirstOrDefault(p => DinoBookCache.IsDinoFile(p) && File.Exists(p))
            : null;
    }
}
