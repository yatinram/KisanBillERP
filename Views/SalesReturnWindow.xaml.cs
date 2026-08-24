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
    public partial class SalesReturnWindow : Window
    {
        private readonly ObservableCollection<SalesReturnItem> _items = new ObservableCollection<SalesReturnItem>();
        private Invoice _selectedInvoice = null;
        private bool _isViewOnly = false;

        public SalesReturnWindow(int returnId = 0, bool viewOnly = false)
        {
            InitializeComponent();
            GridItems.ItemsSource = _items;
            _isViewOnly = viewOnly;

            if (returnId > 0)
            {
                LoadSalesReturn(returnId);
                if (viewOnly) EnableViewOnlyMode();
            }
        }

        private void EnableViewOnlyMode()
        {
            TxtHeaderTitle.Text = "View Sales Return";
            TxtHeaderSubtitle.Text = "Sales return record reference";
            Title = "View Sales Return";

            BtnSelectInvoice.Visibility = Visibility.Collapsed;
            BtnSaveReturn.Visibility = Visibility.Collapsed;

            CmbAdjustmentType.IsEnabled = false;
            CmbReason.IsEnabled = false;
            TxtNotes.IsEnabled = false;
        }

        private void BtnSelectInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewOnly) return;
            try
            {
                var selectWin = new InvoiceSelectForReturnWindow();
                selectWin.Owner = this;
                if (selectWin.ShowDialog() == true && selectWin.SelectedInvoice != null)
                {
                    LoadInvoiceForReturn(selectWin.SelectedInvoice.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting invoice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadInvoiceForReturn(int invoiceId)
        {
            var inv = DatabaseHelper.GetInvoiceById(invoiceId);
            if (inv == null) return;

            _selectedInvoice = inv;
            TxtFarmerName.Text = string.IsNullOrWhiteSpace(inv.CustomerName) ? "Walk-in Farmer" : inv.CustomerName;
            TxtInvoiceNo.Text = inv.InvoiceNo;
            TxtInvoiceDate.Text = inv.InvoiceDate.ToString("dd MMM yyyy");
            TxtUdharBalance.Text = inv.PayableAmount.ToString("N2");

            var rawItems = DatabaseHelper.GetInvoiceItems(invoiceId);
            _items.Clear();

            int idx = 1;
            foreach (var ii in rawItems)
            {
                int alreadyReturned = DatabaseHelper.GetAlreadyReturnedSalesQty(ii.Id);
                int returnable = Math.Max(0, ii.Qty - alreadyReturned);

                var item = new SalesReturnItem
                {
                    SalesReturnItemId = idx++,
                    InvoiceItemId = ii.Id,
                    ProductId = ii.ProductId,
                    ProductName = ii.ProductName ?? "Product",
                    Company = ii.Company ?? "",
                    BatchNumber = ii.BatchNo ?? "",
                    ExpiryDate = ii.ExpiryDate,
                    PurchasedQuantity = ii.Qty,
                    AlreadyReturnedQuantity = alreadyReturned,
                    ReturnableQuantity = returnable,
                    ReturnQuantity = 0,
                    Rate = ii.Rate,
                    GstPercent = ii.GstPercent,
                    Amount = 0m
                };

                _items.Add(item);
            }

            RecalculateSummary();
        }

        private void ReturnQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isViewOnly) return;
            if (sender is TextBox tb && tb.DataContext is SalesReturnItem item)
            {
                if (int.TryParse(tb.Text?.Trim(), out int qty))
                {
                    if (qty < 0)
                    {
                        MessageBox.Show("Return quantity cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        item.ReturnQuantity = 0;
                        tb.Text = "0";
                        return;
                    }

                    if (qty > item.ReturnableQuantity)
                    {
                        MessageBox.Show($"Return quantity for {item.ProductName} cannot exceed the available returnable quantity ({item.ReturnableQuantity}).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        item.ReturnQuantity = item.ReturnableQuantity;
                        tb.Text = item.ReturnableQuantity.ToString();
                        return;
                    }

                    item.ReturnQuantity = qty;
                    item.Amount = qty * item.Rate;
                    RecalculateSummary();
                }
                else if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    item.ReturnQuantity = 0;
                    item.Amount = 0m;
                    RecalculateSummary();
                }
            }
        }

        private void RecalculateSummary()
        {
            if (TxtSubTotal == null || TxtGstAmount == null || TxtRoundOff == null || TxtGrandTotal == null) return;

            decimal subtotal = 0m;
            decimal totalGst = 0m;

            foreach (var item in _items)
            {
                decimal itemBasic = item.ReturnQuantity * item.Rate;
                item.Amount = itemBasic;
                subtotal += itemBasic;
                decimal itemGst = Math.Round(itemBasic * (item.GstPercent / 100m), 2, MidpointRounding.AwayFromZero);
                totalGst += itemGst;
            }

            decimal rawGrandTotal = subtotal + totalGst;
            decimal grandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = grandTotal - rawGrandTotal;

            TxtSubTotal.Text = $"₹ {subtotal:N2}";
            TxtGstAmount.Text = $"₹ {totalGst:N2}";
            TxtRoundOff.Text = $"₹ {roundOff:N2}";
            TxtGrandTotal.Text = $"₹ {grandTotal:N2}";
        }

        private void BtnSaveReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedInvoice == null)
            {
                MessageBox.Show("Please select an invoice first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var returnItems = _items.Where(i => i.ReturnQuantity > 0).ToList();
            if (returnItems.Count == 0)
            {
                MessageBox.Show("Please enter a return quantity (> 0) for at least one product.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                decimal subtotal = returnItems.Sum(i => i.ReturnQuantity * i.Rate);
                decimal totalGst = returnItems.Sum(i => Math.Round(i.ReturnQuantity * i.Rate * (i.GstPercent / 100m), 2, MidpointRounding.AwayFromZero));
                decimal rawGrandTotal = subtotal + totalGst;
                decimal grandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
                decimal roundOff = grandTotal - rawGrandTotal;

                string returnNo = DatabaseHelper.GenerateNextSalesReturnNo();
                string adjType = (CmbAdjustmentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Udhar / Bill Balance Adjustment";
                string reason = (CmbReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other";

                var returnHeader = new SalesReturn
                {
                    ReturnNumber = returnNo,
                    InvoiceId = _selectedInvoice.Id,
                    InvoiceNo = _selectedInvoice.InvoiceNo,
                    FarmerId = _selectedInvoice.FarmerId,
                    FarmerName = _selectedInvoice.CustomerName,
                    MobileNumber = _selectedInvoice.MobileNumber,
                    VillageName = _selectedInvoice.VillageName,
                    ReturnDate = DateTime.Now,
                    SubTotal = subtotal,
                    Discount = 0m,
                    TaxableAmount = subtotal,
                    GSTAmount = totalGst,
                    RoundOff = roundOff,
                    GrandTotal = grandTotal,
                    AdjustmentType = adjType,
                    ReturnReason = reason,
                    Notes = TxtNotes.Text?.Trim()
                };

                int savedId = DatabaseHelper.SaveSalesReturn(returnHeader, returnItems);
                MessageBox.Show($"Sales Return saved successfully.\nReturn Number: {returnNo}\nStock restored to inventory.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sales return: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSalesReturn(int returnId)
        {
            var sr = DatabaseHelper.GetSalesReturnById(returnId);
            if (sr == null) return;

            TxtFarmerName.Text = sr.FarmerName;
            TxtInvoiceNo.Text = sr.InvoiceNo;
            TxtInvoiceDate.Text = sr.ReturnDate.ToString("dd MMM yyyy");
            TxtNotes.Text = sr.Notes;

            var items = DatabaseHelper.GetSalesReturnItems(returnId);
            _items.Clear();
            int idx = 1;
            foreach (var it in items)
            {
                it.SalesReturnItemId = idx++;
                _items.Add(it);
            }

            RecalculateSummary();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
