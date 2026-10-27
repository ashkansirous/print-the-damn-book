using System.IO;
using System.Windows;
using System.Windows.Controls;
using PrintTheDamnBook.App.Services;
using PrintTheDamnBook.App.ViewModels;

namespace PrintTheDamnBook.App.Views;

public partial class OpenStepView : UserControl
{
    public OpenStepView()
    {
        InitializeComponent();
    }

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

    private static string? DroppedDino(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths.FirstOrDefault(p => DinoBookCache.IsDinoFile(p) && File.Exists(p))
            : null;
    }
}
