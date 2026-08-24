using System;
using System.Windows;
using System.Windows.Controls;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = DatabaseHelper.GetCompanySettings();
                if (settings != null)
                {
                    TxtShopName.Text = settings.ShopName ?? string.Empty;
                    TxtShopAddress.Text = settings.ShopAddress ?? string.Empty;
                    TxtShopPhone.Text = settings.ShopPhone ?? string.Empty;
                    TxtGSTIN.Text = settings.GSTIN ?? string.Empty;
                    TxtLicenseNumber.Text = settings.LicenseNumber ?? string.Empty;

                    TxtBankName.Text = settings.BankName ?? string.Empty;
                    TxtAccountName.Text = settings.AccountName ?? string.Empty;
                    TxtAccountNumber.Text = settings.AccountNumber ?? string.Empty;
                    TxtIFSCCode.Text = settings.IFSCCode ?? string.Empty;
                    TxtUpiId.Text = settings.UpiId ?? string.Empty;

                    TxtTerms.Text = settings.TermsAndConditions ?? string.Empty;
                    TxtFooter.Text = settings.FooterMessage ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtShopName.Text))
            {
                MessageBox.Show("Shop Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtShopName.Focus();
                return;
            }

            try
            {
                var settings = new CompanySettings
                {
                    ShopName = TxtShopName.Text.Trim(),
                    ShopAddress = TxtShopAddress.Text.Trim(),
                    ShopPhone = TxtShopPhone.Text.Trim(),
                    GSTIN = TxtGSTIN.Text.Trim(),
                    LicenseNumber = TxtLicenseNumber.Text.Trim(),

                    BankName = TxtBankName.Text.Trim(),
                    AccountName = TxtAccountName.Text.Trim(),
                    AccountNumber = TxtAccountNumber.Text.Trim(),
                    IFSCCode = TxtIFSCCode.Text.Trim(),
                    UpiId = TxtUpiId.Text.Trim(),

                    TermsAndConditions = TxtTerms.Text.Trim(),
                    FooterMessage = TxtFooter.Text.Trim(),
                    UpdatedAt = DateTime.Now
                };

                DatabaseHelper.SaveCompanySettings(settings);
                MessageBox.Show("Company settings saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // If this view is hosted inside the DashboardWindow, refresh its header immediately
                var wnd = Window.GetWindow(this) as DashboardWindow;
                wnd?.RefreshCompanyInfo(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
