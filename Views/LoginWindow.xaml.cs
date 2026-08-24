using System.Windows;
using System.Windows.Input;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnLogin_Click(sender, e);
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPasswordVisible.Visibility == Visibility.Collapsed)
            {
                // Show password as plain text
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Hide password again
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = !string.IsNullOrEmpty(TxtPassword.Password) 
                ? TxtPassword.Password 
                : TxtPasswordVisible.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            var user = DatabaseHelper.ValidateLogin(username, password);
            if (user == null)
            {
                ShowError("Invalid username or password. Please try again.");
                TxtPassword.Password = "";
                TxtPasswordVisible.Text = "";
                return;
            }

            // Successful login - set session user and open dashboard
            AppSession.CurrentUser = user;
            var dashboard = new DashboardWindow(user);
            dashboard.Show();
            this.Close();
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}