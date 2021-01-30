using System.Windows;
using System.Windows.Controls;

namespace MaFi.WebShareCz.TcPlugin.UI
{
    /// <summary>
    /// Interaction logic for UserCredentialDialog.xaml
    /// </summary>
    internal partial class UserCredentialDialog : Window
    {
        public UserCredentialDialog(string userName = null)
        {
            InitializeComponent();
            this.OkButton.Click += (RoutedEventHandler)((param0, param1) => DialogResult = true);
            this.TxtPassword.PasswordChanged += (RoutedEventHandler)((param0, param1) => EnableOkButtonIfCredentialsFilled());
            if (string.IsNullOrWhiteSpace(userName))
            {
                this.TxtUserName.TextChanged += (TextChangedEventHandler)((param0, param1) => EnableOkButtonIfCredentialsFilled());
                this.TxtUserName.Focus();
            }
            else
            {
                this.TxtUserName.Text = userName;
                this.TxtUserName.IsReadOnly = true;
                this.TxtPassword.Focus();
            }
        }

        private void EnableOkButtonIfCredentialsFilled()
        {
            this.OkButton.IsEnabled = this.TxtUserName.Text != string.Empty && this.TxtPassword.Password != string.Empty;
        }
    }
}
