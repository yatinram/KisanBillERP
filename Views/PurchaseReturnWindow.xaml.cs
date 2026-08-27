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
    public partial class PurchaseReturnWindow : Window
    {
        private List<Purchase> _purchaseCandidates = new List<Purchase>();
        private readonly ObservableCollection<PurchaseReturnItem> _items = new ObservableCollection<PurchaseReturnItem>();
        private Purchase _selectedPurchase = null;
        private bool _isViewOnly = false;
        private int _viewReturnId = 0;

        public PurchaseReturnWindow(int returnId = 0, bool viewOnly = false)
        {
            InitializeComponent();
            GridItems.ItemsSource = _items;
            _isViewOnly = viewOnly;
            _viewReturnId = returnId;

            DpReturnDate.SelectedDate = DateTime.Now;

            if (returnId > 0)
            {
                LoadPurchaseReturn(returnId);
                if (viewOnly)
                {
                    EnableViewOnlyMode();
                }

            }

        }

        private void CmbSelectPurchase_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                var box = sender as ComboBox;
                var txt = box.Text?.Trim();
                _purchaseCandidates = DatabaseHelper.GetPurchasesForReturnSelection(txt);
                var items = _purchaseCandidates.Select(p => new { p.PurchaseId, Display = $"{p.PurchaseNumber} | {p.SupplierName} | {p.SupplierInvoiceNumber}" }).ToList();
                box.ItemsSource = items;
                box.IsDropDownOpen = items.Count > 0;
            }
            catch { }
        }

        private void CmbSelectPurchase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbSelectPurchase.SelectedItem == null) return;
            try
            {
                dynamic sel = CmbSelectPurchase.SelectedItem;
                int pid = sel.PurchaseId;
                LoadPurchaseForReturn(pid);
            }
            catch { }
        }

        private void EnableViewOnlyMode()
        {
            TxtHeaderTitle.Text = "View Purchase Return";
            TxtHeaderSubtitle.Text = "Purchase return record reference";
            Title = "View Purchase Return";

            // Disable the selection combo in view-only mode
            if (CmbSelectPurchase != null) CmbSelectPurchase.IsEnabled = false;
            BtnSaveReturn.Visibility = Visibility.Collapsed;

            DpReturnDate.IsEnabled = false;
            CmbReason.IsEnabled = false;
            TxtOtherReason.IsEnabled = false;
            TxtNotes.IsEnabled = false;
            // ColAction may have been removed from XAML in some views; safely hide if present
            var colActionElement = this.FindName("ColAction") as UIElement;
            if (colActionElement != null)
            {
                colActionElement.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSelectPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewOnly) return;
            try
            {
                var selectWin = new PurchaseSelectWindow();
                selectWin.Owner = this;
                if (selectWin.ShowDialog() == true && selectWin.SelectedPurchase != null)
                {
                    LoadPurchaseForReturn(selectWin.SelectedPurchase.PurchaseId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting purchase: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadPurchaseForReturn(int purchaseId)
        {
            var p = DatabaseHelper.GetPurchaseById(purchaseId);
            if (p == null) return;

            _selectedPurchase = p;
            if (CmbSelectPurchase != null)
            {
                CmbSelectPurchase.Text = p.PurchaseNumber;
            }
            TxtSupplierName.Text = p.SupplierName;
            TxtSupplierInvoice.Text = p.SupplierInvoiceNumber;
            TxtPaperBill.Text = p.PaperBillNumber;
            TxtPurchaseDate.Text = p.PurchaseDate.ToString("dd MMM yyyy");

            // Load items from original purchase
            var rawItems = DatabaseHelper.GetPurchaseItems(purchaseId);
            _items.Clear();

            int idx = 1;
            foreach (var pi in rawItems)
            {
                int alreadyReturned = DatabaseHelper.GetAlreadyReturnedQty(pi.PurchaseItemId);
                int maxByPurchase = Math.Max(0, pi.Quantity - alreadyReturned);

                // Cannot return more than current stock (items already sold cannot be returned to supplier)
                int currentStock = DatabaseHelper.GetProductCurrentStock(pi.ProductId);
                int returnable = Math.Min(maxByPurchase, currentStock);

                var item = new PurchaseReturnItem
                {
                    PurchaseReturnItemId = idx++,
                    PurchaseItemId = pi.PurchaseItemId,
                    ProductId = pi.ProductId,
                    ProductName = pi.ProductName,
                    Company = pi.Company,
                    BatchNumber = pi.BatchNumber,
                    ExpiryDate = pi.ExpiryDate,
                    PurchasedQuantity = pi.Quantity,
                    AlreadyReturnedQuantity = alreadyReturned,
                    ReturnableQuantity = returnable,
                    ReturnQuantity = 0, // default to 0, user enters quantity to return
                    PurchasePrice = pi.PurchasePrice,
                    GST = pi.GST,
                    Amount = 0m
                };

                _items.Add(item);
            }

            if (BorderEmptyNotice != null)
            {
                BorderEmptyNotice.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (_items.Count == 0)
                {
                    TxtEmptyNotice.Text = "No returnable items are available for this purchase.";
                }
            }

            RecalculateSummary();
        }

        private void ReturnQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isViewOnly) return;
            if (sender is TextBox tb && tb.DataContext is PurchaseReturnItem item)
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
                    item.Amount = qty * item.PurchasePrice;
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

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewOnly) return;
            if (sender is Button btn && btn.DataContext is PurchaseReturnItem item)
            {
                _items.Remove(item);
                int idx = 1;
                foreach (var it in _items) it.PurchaseReturnItemId = idx++;
                RecalculateSummary();
            }
        }

        private void CmbReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelOtherReason == null) return;
            if (CmbReason.SelectedItem is ComboBoxItem item)
            {
                string reason = item.Content?.ToString() ?? "";
                PanelOtherReason.Visibility = reason == "Other" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RecalculateSummary()
        {
            if (TxtSubTotal == null || TxtTaxableAmount == null || TxtGstAmount == null ||
                TxtRoundOff == null || TxtGrandTotal == null) return;

            decimal subtotal = 0m;
            decimal totalGst = 0m;

            foreach (var item in _items)
            {
                decimal itemBasic = item.ReturnQuantity * item.PurchasePrice;
                item.Amount = itemBasic;
                subtotal += itemBasic;

                decimal itemGst = Math.Round(itemBasic * (item.GST / 100m), 2, MidpointRounding.AwayFromZero);
                totalGst += itemGst;
            }

            decimal discount = 0m; // Purchase return follows original purchase pricing
            decimal taxableAmount = Math.Max(0m, subtotal - discount);

            decimal rawGrandTotal = taxableAmount + totalGst;
            decimal roundedGrandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = roundedGrandTotal - rawGrandTotal;

            TxtSubTotal.Text = $"₹ {subtotal:N2}";
            TxtDiscount.Text = $"₹ {discount:N2}";
            TxtTaxableAmount.Text = $"₹ {taxableAmount:N2}";
            TxtGstAmount.Text = $"₹ {totalGst:N2}";
            TxtRoundOff.Text = $"₹ {roundOff:N2}";
            TxtGrandTotal.Text = $"₹ {roundedGrandTotal:N2}";
        }

        private void BtnSaveReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPurchase == null)
            {
                MessageBox.Show("Please select a purchase.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (CmbSelectPurchase != null) CmbSelectPurchase.Focus();
                return;
            }

            if (!DpReturnDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Return date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpReturnDate.Focus();
                return;
            }

            var activeItems = _items.Where(i => i.ReturnQuantity > 0).ToList();
            if (activeItems.Count == 0)
            {
                MessageBox.Show("Please enter return quantity for at least one item.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine Return Reason
            string reason = (CmbReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (reason == "Other")
            {
                reason = TxtOtherReason.Text?.Trim();
                if (string.IsNullOrWhiteSpace(reason))
                {
                    MessageBox.Show("Please specify the return reason.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtOtherReason.Focus();
                    return;
                }
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("Please select a return reason.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbReason.Focus();
                return;
            }

            // Re-validate stock for batch-wise return
            foreach (var item in activeItems)
            {
                var prod = DatabaseHelper.GetProductById(item.ProductId);
                if (prod != null && item.ReturnQuantity > prod.StockQty)
                {
                    MessageBox.Show($"Return quantity for {item.ProductName} ({item.ReturnQuantity}) cannot exceed the available batch stock ({prod.StockQty}).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Recalculate totals for active items
            decimal subtotal = activeItems.Sum(i => i.ReturnQuantity * i.PurchasePrice);
            decimal totalGst = activeItems.Sum(i => Math.Round((i.ReturnQuantity * i.PurchasePrice) * (i.GST / 100m), 2, MidpointRounding.AwayFromZero));
            decimal rawGrandTotal = subtotal + totalGst;
            decimal grandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = grandTotal - rawGrandTotal;

            string returnNo = DatabaseHelper.GenerateNextPurchaseReturnNo();

            var returnHeader = new PurchaseReturn
            {
                ReturnNumber = returnNo,
                PurchaseId = _selectedPurchase.PurchaseId,
                PurchaseNumber = _selectedPurchase.PurchaseNumber,
                SupplierId = _selectedPurchase.SupplierId,
                SupplierName = _selectedPurchase.SupplierName,
                SupplierInvoiceNumber = _selectedPurchase.SupplierInvoiceNumber,
                PaperBillNumber = _selectedPurchase.PaperBillNumber,
                ReturnDate = DpReturnDate.SelectedDate.Value,
                SubTotal = subtotal,
                Discount = 0m,
                TaxableAmount = subtotal,
                GSTAmount = totalGst,
                RoundOff = roundOff,
                GrandTotal = grandTotal,
                ReturnReason = reason,
                Notes = TxtNotes.Text?.Trim(),
                Status = "Completed"
            };

            BtnSaveReturn.IsEnabled = false;
            TxtSavingStatus.Text = "Saving purchase return...";

            try
            {
                int savedId = DatabaseHelper.SavePurchaseReturn(returnHeader, activeItems);
                MessageBox.Show($"Purchase return saved successfully.\nReturn Number: {returnNo}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving purchase return: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSaveReturn.IsEnabled = true;
                TxtSavingStatus.Text = string.Empty;
            }
        }

        private void LoadPurchaseReturn(int returnId)
        {
            var r = DatabaseHelper.GetPurchaseReturnById(returnId);
            if (r == null) return;

            if (CmbSelectPurchase != null) CmbSelectPurchase.Text = r.PurchaseNumber;
            TxtSupplierName.Text = r.SupplierName;
            TxtSupplierInvoice.Text = r.SupplierInvoiceNumber;
            TxtPaperBill.Text = r.PaperBillNumber;
            DpReturnDate.SelectedDate = r.ReturnDate;
            TxtNotes.Text = r.Notes;

            // Select reason
            foreach (ComboBoxItem item in CmbReason.Items)
            {
                if (item.Content?.ToString() == r.ReturnReason)
                {
                    CmbReason.SelectedItem = item;
                    break;
                }
            }

            var items = DatabaseHelper.GetPurchaseReturnItems(returnId);
            _items.Clear();
            int idx = 1;
            foreach (var it in items)
            {
                it.PurchaseReturnItemId = idx++;
                _items.Add(it);
            }

            if (BorderEmptyNotice != null)
            {
                BorderEmptyNotice.Visibility = Visibility.Collapsed;
            }

            RecalculateSummary();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
