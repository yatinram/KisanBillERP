using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class InvoiceEditWindow : Window
    {
        private readonly ObservableCollection<InvoiceItem> _items = new ObservableCollection<InvoiceItem>();
        private Invoice _invoice;
        private List<Product> _products;

        public InvoiceEditWindow(int invoiceId)
        {
            InitializeComponent();
            GridItems.ItemsSource = _items;
            LoadInvoice(invoiceId);
        }

        private void LoadInvoice(int invoiceId)
        {
            _invoice = DatabaseHelper.GetInvoiceById(invoiceId);
            if (_invoice == null)
            {
                MessageBox.Show("Invoice record not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            TxtInvoiceNo.Text = _invoice.InvoiceNo;
            TxtInvoiceDate.Text = _invoice.InvoiceDate.ToString("dd MMM yyyy, hh:mm tt");

            // Load Customers dropdown
            var customers = DatabaseHelper.GetCustomers();
            customers.Insert(0, new Customer { Id = 0, Name = "Walk-in Customer" });
            CmbCustomer.ItemsSource = customers;

            var matchCust = customers.FirstOrDefault(c => c.Id == _invoice.CustomerId);
            CmbCustomer.SelectedItem = matchCust ?? customers[0];

            // Select payment method
            foreach (ComboBoxItem item in CmbPaymentMethod.Items)
            {
                if (item.Content?.ToString() == _invoice.PaymentMethod)
                {
                    CmbPaymentMethod.SelectedItem = item;
                    break;
                }
            }

            // Load products dropdown
            _products = DatabaseHelper.GetProducts();
            CmbProduct.ItemsSource = _products;

            // Load existing items
            var items = DatabaseHelper.GetInvoiceItems(invoiceId);
            _items.Clear();
            foreach (var it in items)
            {
                _items.Add(it);
            }

            RecalculateTotals();
        }

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Product p)
            {
                TxtAddRate.Text = p.SalePrice.ToString("F2");
            }
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (CmbProduct.SelectedItem is not Product p)
            {
                MessageBox.Show("Pehla product select karo.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!int.TryParse(TxtAddQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Valid Qty nakho.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            decimal.TryParse(TxtAddRate.Text, out decimal rate);
            decimal amount = qty * rate;
            amount += amount * (p.GstPercent / 100m);

            _items.Add(new InvoiceItem
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Qty = qty,
                Rate = rate,
                GstPercent = p.GstPercent,
                Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero)
            });

            RecalculateTotals();
            TxtAddQty.Text = "1";
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is InvoiceItem item)
            {
                _items.Remove(item);
                RecalculateTotals();
            }
        }

        private void RecalculateTotals()
        {
            if (TxtSubTotal == null || TxtGstTotal == null || TxtGrandTotal == null) return;

            decimal subTotal = _items.Sum(i => i.Qty * i.Rate);
            decimal gstTotal = _items.Sum(i => (i.Qty * i.Rate) * (i.GstPercent / 100m));
            decimal grand = subTotal + gstTotal;

            TxtSubTotal.Text = $"₹ {subTotal:N2}";
            TxtGstTotal.Text = $"₹ {gstTotal:N2}";
            TxtGrandTotal.Text = $"₹ {grand:N2}";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Invoice ma ochu ek item umerelu hovu joiye.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var customer = CmbCustomer.SelectedItem as Customer;
            string pm = (CmbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";

            decimal subTotal = _items.Sum(i => i.Qty * i.Rate);
            decimal gstTotal = _items.Sum(i => (i.Qty * i.Rate) * (i.GstPercent / 100m));

            _invoice.CustomerId = customer?.Id ?? 0;
            _invoice.SubTotal = subTotal;
            _invoice.GstAmount = gstTotal;
            _invoice.GrandTotal = subTotal + gstTotal;
            _invoice.PaymentMethod = pm;

            try
            {
                DatabaseHelper.UpdateInvoice(_invoice, _items.ToList());
                MessageBox.Show($"Invoice '{_invoice.InvoiceNo}' updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating invoice: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
