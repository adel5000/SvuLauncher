using System.ComponentModel;
using SvuLauncher.App.Resources;
using SvuLauncher.App.ViewModels;
using Wpf.Ui.Controls;

namespace SvuLauncher.App;

public partial class MainWindow : FluentWindow
{
    private bool _closeConfirmed;

    public MainWindow() => InitializeComponent();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed || DataContext is not ShellViewModel { HasRunningSession: true } shell)
            return;

        e.Cancel = true;
        var dialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = Strings.AppTitle,
            Content = Strings.Close_Confirm,
            PrimaryButtonText = Strings.Dialog_Yes,
            CloseButtonText = Strings.Dialog_No,
            FlowDirection = FlowDirection,
            Owner = this,
        };
        if (await dialog.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        await shell.EndSessionAsync();
        _closeConfirmed = true;
        Close();
    }
}
