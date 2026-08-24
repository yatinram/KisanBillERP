using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class InvoiceSelectForReturnWindow : Window
    {
        public Invoice SelectedInvoice { get; private set; }

        public InvoiceSelectForReturnWindow()
        {
            InitializeComponent();
            LoadInvoices();
        }

        private void LoadInvoices()
        {
            string search = TxtSearch.Text?.Trim();
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            var res = DatabaseHelper.GetInvoicesPaged(search: search, customerId: 0, farmerId: 0, paymentMethod: "All", dateRange: "All", customStart: null, customEnd: null, page: 1, pageSize: 50);
            GridInvoices.ItemsSource = res.Items;
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            LoadInvoices();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Invoice inv)
            {
                SelectedInvoice = inv;
                DialogResult = true;
                Close();
            }
        }

        private void GridInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridInvoices.SelectedItem is Invoice inv)
            {
                SelectedInvoice = inv;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
