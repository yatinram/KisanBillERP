using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KrushiBillERP.Models;
using KrushiBillERP.Data;

namespace KrushiBillERP.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly User _currentUser;
        private readonly DispatcherTimer _clockTimer;
        private Button _currentActiveBtn;

        public DashboardWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            this.Title = $"KrushiBill ERP - {_currentUser.ShopName}";
            TxtShopName.Text = _currentUser.ShopName;
            TxtShopAddress.Text = $"{_currentUser.ShopAddress}  |  {_currentUser.ShopPhone}";
            TxtUserName.Text = _currentUser.FullName;
            TxtUserRole.Text = string.IsNullOrEmpty(_currentUser.Role) ? "Operator" : _currentUser.Role;
            TxtUserInitial.Text = string.IsNullOrEmpty(_currentUser.FullName) ? "?" : _currentUser.FullName.Substring(0, 1).ToUpper();

            // Start Live Clock
            UpdateClock();
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();

            // F2 Key Shortcut for Quick Billing
            this.KeyDown += DashboardWindow_KeyDown;

            // Load initial view
            NavDashboard_Click(NavDashboard, null);
        }

        private void UpdateClock()
        {
            if (TxtHeaderClock != null)
                TxtHeaderClock.Text = DateTime.Now.ToString("dd MMM yyyy, hh:mm:ss tt");
        }

        private void DashboardWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                NavBilling_Click(NavBilling, null);
            }
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavDashboard, new DashboardHomeView(_currentUser));
        private void NavProducts_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavProducts, new ProductsView());
        private void NavFarmers_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavFarmers, new FarmersView());
        private void NavBilling_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavBilling, new BillingView());
        private void NavCustomers_Click(object sender, RoutedEventArgs e) => SafeNavigate(null, new CustomersView());
        private void NavSuppliers_Click(object sender, RoutedEventArgs e) => SafeNavigate(null, new SuppliersView());
        private void NavPurchase_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavPurchase, new PurchaseView());
        private void NavPurchaseReturn_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavPurchaseReturn, new PurchaseReturnView());
        private void NavPaymentReceipts_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavPaymentReceipts, new PaymentReceiptView());
        private void NavStock_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavStock, new StockView());
        private void NavInvoices_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavInvoices, new InvoicesView());
        private void NavSalesReturn_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavSalesReturn, new SalesReturnView());
        private void NavExpenses_Click(object sender, RoutedEventArgs e) => SafeNavigate(null, new ExpensesView());
        private void NavReports_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavReports, new ReportsView());
        private void NavUsers_Click(object sender, RoutedEventArgs e) => SafeNavigate(null, new UsersView());
        private void NavSettings_Click(object sender, RoutedEventArgs e) => SafeNavigate(NavSettings, new SettingsView());

        private void SafeNavigate(Button btn, object page)
        {
            try
            {
                ContentFrame.Navigate(page);
                SetActiveNavButton(btn);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation Error: {ex.Message}\n\nDetails: {ex.InnerException?.Message ?? ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetActiveNavButton(Button activeBtn)
        {
            if (activeBtn == null) return;

            var navButtons = new List<Button>
            {
                NavDashboard, NavBilling, NavInvoices, NavSalesReturn, NavPaymentReceipts,
                NavFarmers, NavProducts, NavStock, NavPurchase, NavPurchaseReturn,
                NavReports, NavSettings
            };

            foreach (var b in navButtons)
            {
                if (b == null) continue;
                if (b == activeBtn)
                {
                    b.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E4D3B"));
                    b.Foreground = new SolidColorBrush(Colors.White);
                    b.FontWeight = FontWeights.Bold;

                    // Show active pill indicator inside template if found
                    SetPillVisibility(b, Visibility.Visible);
                }
                else
                {
                    b.Background = Brushes.Transparent;
                    b.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                    b.FontWeight = FontWeights.Normal;
                    SetPillVisibility(b, Visibility.Collapsed);
                }
            }

            _currentActiveBtn = activeBtn;
        }

        private void SetPillVisibility(Button btn, Visibility vis)
        {
            try
            {
                var pill = btn.Template?.FindName("ActivePill", btn) as Border;
                if (pill != null)
                {
                    pill.Visibility = vis;
                }
            }
            catch { }
        }

        /// <summary>
        /// Navigate to Billing view and load the specified invoice into the Add Bill section
        /// so editing happens in-page instead of a modal window.
        /// </summary>
        public void OpenBillingViewWithInvoice(int invoiceId)
        {
            try
            {
                var billing = new BillingView();
                ContentFrame.Navigate(billing);
                SetActiveNavButton(NavBilling);
                // Allow UI to render the page before loading data. Lock farmer selection when coming from Bill History.
                billing.LoadInvoiceForEdit(invoiceId, lockFarmer: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening billing editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Tame logout karva mangta cho?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var login = new LoginWindow();
                login.Show();
                this.Close();
            }

        }

        // Called when global company settings change so the main UI updates immediately.
        public void RefreshCompanyInfo(Models.CompanySettings settings)
        {
            try
            {
                if (settings == null) return;
                this.Title = $"KrushiBill ERP - {settings.ShopName}";
                TxtShopName.Text = settings.ShopName;
                TxtShopAddress.Text = $"{settings.ShopAddress}  |  {settings.ShopPhone}";
            }
            catch
            {
                // Swallow any refresh errors to avoid breaking caller UI flow
            }
        }
    }
}
