using System.Windows;
using System.Windows.Controls;

namespace SvuLauncher.App.Views;

public partial class LoginView : UserControl
{
    public LoginView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        (string.IsNullOrEmpty(UserNameBox.Text) ? (Control)UserNameBox : PasswordBox).Focus();
}
