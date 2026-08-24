using System;
using System.Windows;
using KrushiBillERP.Models;
using KrushiBillERP.Data;

namespace KrushiBillERP.Views
{
    public partial class DashboardWindow : Window
    {
        private readonly User _currentUser;

        public DashboardWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;

            // Everything below is driven by whichever user just logged in -
            // no shop name / user data is hard-coded, so the same EXE works for any shop.
            this.Title = $"KrushiBill ERP - {_currentUser.ShopName}";
            TxtShopName.Text = _currentUser.ShopName;
            TxtShopAddress.Text = $"{_currentUser.ShopAddress}  |  {_currentUser.ShopPhone}";
            TxtUserName.Text = $"{_currentUser.FullName} ({_currentUser.Role})";
            TxtUserInitial.Text = string.IsNullOrEmpty(_currentUser.FullName) ? "?" : _currentUser.FullName.Substring(0, 1).ToUpper();

            ContentFrame.Navigate(new DashboardHomeView(_currentUser));
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e) => SafeNavigate(new DashboardHomeView(_currentUser));
        private void NavProducts_Click(object sender, RoutedEventArgs e) => SafeNavigate(new ProductsView());
        private void NavFarmers_Click(object sender, RoutedEventArgs e) => SafeNavigate(new FarmersView());
        private void NavBilling_Click(object sender, RoutedEventArgs e) => SafeNavigate(new BillingView());
        private void NavCustomers_Click(object sender, RoutedEventArgs e) => SafeNavigate(new CustomersView());
        private void NavSuppliers_Click(object sender, RoutedEventArgs e) => SafeNavigate(new SuppliersView());
        private void NavPurchase_Click(object sender, RoutedEventArgs e) => SafeNavigate(new PurchaseView());
        private void NavPurchaseReturn_Click(object sender, RoutedEventArgs e) => SafeNavigate(new PurchaseReturnView());
        private void NavPaymentReceipts_Click(object sender, RoutedEventArgs e) => SafeNavigate(new PaymentReceiptView());
        private void NavStock_Click(object sender, RoutedEventArgs e) => SafeNavigate(new StockView());
        private void NavInvoices_Click(object sender, RoutedEventArgs e) => SafeNavigate(new InvoicesView());
        private void NavSalesReturn_Click(object sender, RoutedEventArgs e) => SafeNavigate(new SalesReturnView());
        private void NavExpenses_Click(object sender, RoutedEventArgs e) => SafeNavigate(new ExpensesView());
        private void NavReports_Click(object sender, RoutedEventArgs e) => SafeNavigate(new ReportsView());
        private void NavUsers_Click(object sender, RoutedEventArgs e) => SafeNavigate(new UsersView());
        private void NavSettings_Click(object sender, RoutedEventArgs e) => SafeNavigate(new SettingsView());

        private void SafeNavigate(object page)
        {
            try
            {
                ContentFrame.Navigate(page);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation Error: {ex.Message}\n\nDetails: {ex.InnerException?.Message ?? ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
