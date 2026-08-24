using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class PurchaseEntryWindow : Window
    {
        private readonly ObservableCollection<PurchaseItem> _items = new ObservableCollection<PurchaseItem>();
        private bool _isViewOnly = false;
        private int _viewPurchaseId = 0;
        private int _itemCounter = 1;

        public PurchaseEntryWindow(int purchaseId = 0, bool viewOnly = false)
        {
            InitializeComponent();
            GridItems.ItemsSource = _items;
            _isViewOnly = viewOnly;
            _viewPurchaseId = purchaseId;

            PopulateDropdowns();
            DpPurchaseDate.SelectedDate = DateTime.Now;

            if (purchaseId > 0)
            {
                LoadPurchase(purchaseId);
                if (viewOnly)
                {
                    EnableViewOnlyMode();
                }
            }
            else
            {
                TxtBatch.Text = DatabaseHelper.GenerateNextBatchNumber();
            }
        }

        private void PopulateDropdowns()
        {
            // Populate Categories
            try
            {
                var categories = DatabaseHelper.GetCategories();
                CmbCategory.ItemsSource = categories.Select(c => c.Name).ToList();
            }
            catch { }

            // Populate Standard Units
            var units = new List<string> { "Kg", "Ltr", "Ml", "Gm", "Nos", "Bottle", "Packet", "Bag", "Tin", "Dose", "Pouch" };
            CmbUnit.ItemsSource = units;
        }

        

        private void EnableViewOnlyMode()
        {
            TxtHeaderTitle.Text = "View Purchase Entry";
            TxtHeaderSubtitle.Text = "Supplier purchase details reference";
            Title = "View Purchase Entry";

            CardAddProduct.Visibility = Visibility.Collapsed;
            BtnSavePurchase.Visibility = Visibility.Collapsed;

            TxtSupplierName.IsEnabled = false;
            DpPurchaseDate.IsEnabled = false;
            TxtSupplierInvoice.IsEnabled = false;

            CmbPaymentMode.IsEnabled = false;
            TxtPaidAmount.IsEnabled = false;
            TxtPaymentRef.IsEnabled = false;
            TxtDiscount.IsEnabled = false;
            TxtNotes.IsEnabled = false;

            ColAction.Visibility = Visibility.Collapsed;
        }

        #region Supplier Autocomplete (Normal Text Input with Suggestion Popup)

        private void TxtSupplierName_KeyUp(object sender, KeyEventArgs e)
        {
            if (_isViewOnly) return;
            string search = TxtSupplierName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length < 1)
            {
                PopSupplierSuggestions.IsOpen = false;
                return;
            }

            var suggestions = DatabaseHelper.GetSupplierNameSuggestions(search);
            if (suggestions != null && suggestions.Count > 0)
            {
                LstSupplierSuggestions.ItemsSource = suggestions;
                PopSupplierSuggestions.IsOpen = true;
            }
            else
            {
                PopSupplierSuggestions.IsOpen = false;
            }
        }

        private void LstSupplierSuggestions_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (LstSupplierSuggestions.SelectedItem is string selectedName)
            {
                TxtSupplierName.Text = selectedName;
                PopSupplierSuggestions.IsOpen = false;
            }
        }

        private void TxtSupplierName_LostFocus(object sender, RoutedEventArgs e)
        {
            // delay closing so click event fires if needed
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!LstSupplierSuggestions.IsFocused)
                {
                    PopSupplierSuggestions.IsOpen = false;
                }
            }));
        }

        #endregion

        #region Product Autocomplete & Auto-Fill

        private void TxtProductName_KeyUp(object sender, KeyEventArgs e)
        {
            if (_isViewOnly) return;
            string search = TxtProductName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length < 1)
            {
                PopProductSuggestions.IsOpen = false;
                return;
            }

            var suggestions = DatabaseHelper.GetProductSuggestions(search);
            if (suggestions != null && suggestions.Count > 0)
            {
                LstProductSuggestions.ItemsSource = suggestions;
                PopProductSuggestions.IsOpen = true;
            }
            else
            {
                PopProductSuggestions.IsOpen = false;
            }
        }

        private void LstProductSuggestions_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (LstProductSuggestions.SelectedItem is Product p)
            {
                TxtProductName.Text = p.Name ?? string.Empty;
                TxtCompany.Text = p.Company ?? string.Empty;
                CmbCategory.Text = p.CategoryName ?? string.Empty;
                TxtPackSize.Text = p.PackSize > 0 ? p.PackSize.ToString("G29") : string.Empty;
                CmbUnit.Text = p.Unit ?? string.Empty;
                TxtPurchasePrice.Text = p.PurchasePrice.ToString("N2");
                TxtGST.Text = p.GstPercent.ToString("G29");
                TxtHSN.Text = p.HSN ?? string.Empty;
                TxtBatch.Text = p.BatchNo ?? string.Empty;
                DpExpiry.SelectedDate = p.ExpiryDate;

                PopProductSuggestions.IsOpen = false;
                TxtQty.Focus();
            }
        }

        private void TxtProductName_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!LstProductSuggestions.IsFocused)
                {
                    PopProductSuggestions.IsOpen = false;
                }
            }));
        }

        #endregion

        #region Live Calculations

        private void RecalculateSummary()
        {
            if (TxtSubTotal == null || TxtTaxableAmount == null || TxtGstAmount == null || 
                TxtRoundOff == null || TxtGrandTotal == null || TxtPayableAmount == null) return;

            decimal subtotal = 0m;
            foreach (var item in _items)
            {
                subtotal += item.Quantity * item.PurchasePrice;
            }

            decimal discount = 0m;
            if (TxtDiscount != null && decimal.TryParse(TxtDiscount.Text?.Trim(), out decimal d))
            {
                discount = Math.Max(0m, d);
            }

            decimal taxableAmount = Math.Max(0m, subtotal - discount);

            // Calculate GST per item proportionally if discount applied
            decimal totalGst = 0m;
            foreach (var item in _items)
            {
                decimal itemBasic = item.Quantity * item.PurchasePrice;
                decimal itemDiscountShare = subtotal > 0 ? (itemBasic / subtotal) * discount : 0m;
                decimal itemTaxable = Math.Max(0m, itemBasic - itemDiscountShare);
                decimal itemGst = Math.Round(itemTaxable * (item.GST / 100m), 2, MidpointRounding.AwayFromZero);
                totalGst += itemGst;
            }

            decimal rawGrandTotal = taxableAmount + totalGst;
            decimal roundedGrandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = roundedGrandTotal - rawGrandTotal;

            // Payment & Payable calculation
            decimal paidAmount = 0m;
            if (TxtPaidAmount != null && decimal.TryParse(TxtPaidAmount.Text?.Trim(), out decimal p))
            {
                paidAmount = Math.Max(0m, p);
            }

            decimal payableAmount = Math.Max(0m, roundedGrandTotal - paidAmount);

            // Update UI
            TxtSubTotal.Text = $"₹ {subtotal:N2}";
            TxtTaxableAmount.Text = $"₹ {taxableAmount:N2}";
            TxtGstAmount.Text = $"₹ {totalGst:N2}";
            TxtRoundOff.Text = $"₹ {roundOff:N2}";
            TxtGrandTotal.Text = $"₹ {roundedGrandTotal:N2}";
            TxtPayableAmount.Text = $"₹ {payableAmount:N2}";
        }

        private void ProductInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Optional live preview calculation when entering item quantity / price
        }

        private void TxtDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateSummary();
        }

        private void TxtPaidAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateSummary();
        }

        private void CmbPaymentMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StarPaymentRef == null || TxtPaymentRef == null || TxtPaidAmount == null) return;

            if (CmbPaymentMode.SelectedItem is ComboBoxItem item)
            {
                string mode = item.Content?.ToString() ?? "";
                if (mode.Contains("UPI") || mode.Contains("Online"))
                {
                    if (PanelPaidAmount != null) PanelPaidAmount.Visibility = Visibility.Visible;
                    if (PanelPaymentRef != null) PanelPaymentRef.Visibility = Visibility.Visible;
                    StarPaymentRef.Visibility = Visibility.Visible;
                    TxtPaymentRef.IsEnabled = true;
                }
                else if (mode.Contains("Credit") || mode.Contains("Udhar"))
                {
                    if (PanelPaidAmount != null) PanelPaidAmount.Visibility = Visibility.Collapsed;
                    if (PanelPaymentRef != null) PanelPaymentRef.Visibility = Visibility.Collapsed;
                    StarPaymentRef.Visibility = Visibility.Collapsed;
                    TxtPaidAmount.Text = "0.00";
                }
                else // Cash
                {
                    if (PanelPaidAmount != null) PanelPaidAmount.Visibility = Visibility.Visible;
                    if (PanelPaymentRef != null) PanelPaymentRef.Visibility = Visibility.Collapsed;
                    StarPaymentRef.Visibility = Visibility.Collapsed;
                    TxtPaymentRef.IsEnabled = true;
                }
            }
        }

        #endregion

        #region Product Management in Grid

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            // Validations for Product Entry
            string pName = TxtProductName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(pName))
            {
                MessageBox.Show("Product name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtProductName.Focus();
                return;
            }

            string company = TxtCompany.Text?.Trim();
            if (string.IsNullOrWhiteSpace(company))
            {
                MessageBox.Show("Company name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCompany.Focus();
                return;
            }

            string category = CmbCategory.Text?.Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                MessageBox.Show("Category is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbCategory.Focus();
                return;
            }

            if (!decimal.TryParse(TxtPackSize.Text?.Trim(), out decimal packSize) || packSize <= 0)
            {
                MessageBox.Show("Valid pack size is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPackSize.Focus();
                return;
            }

            string unit = CmbUnit.Text?.Trim();
            if (string.IsNullOrWhiteSpace(unit))
            {
                MessageBox.Show("Unit is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbUnit.Focus();
                return;
            }

            if (!decimal.TryParse(TxtPurchasePrice.Text?.Trim(), out decimal purchasePrice) || purchasePrice < 0)
            {
                MessageBox.Show("Purchase price cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPurchasePrice.Focus();
                return;
            }

            decimal sellingPrice = purchasePrice;

            decimal gst = 0m;
            if (!string.IsNullOrWhiteSpace(TxtGST.Text?.Trim()))
            {
                if (!decimal.TryParse(TxtGST.Text.Trim(), out gst) || gst < 0 || gst > 100)
                {
                    MessageBox.Show("GST percentage must be between 0 and 100.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtGST.Focus();
                    return;
                }
            }

            string batch = TxtBatch.Text?.Trim();
            if (string.IsNullOrWhiteSpace(batch))
            {
                MessageBox.Show("Batch number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtBatch.Focus();
                return;
            }

            if (!DpExpiry.SelectedDate.HasValue)
            {
                MessageBox.Show("Expiry date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpExpiry.Focus();
                return;
            }

            if (!int.TryParse(TxtQty.Text?.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Purchase quantity must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtQty.Focus();
                return;
            }

            int freeQty = 0;
            if (!string.IsNullOrWhiteSpace(TxtFreeQty.Text?.Trim()))
            {
                if (!int.TryParse(TxtFreeQty.Text.Trim(), out freeQty) || freeQty < 0)
                {
                    MessageBox.Show("Free quantity cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtFreeQty.Focus();
                    return;
                }
            }

            decimal itemAmount = qty * purchasePrice;

            var item = new PurchaseItem
            {
                PurchaseItemId = _itemCounter++,
                ProductId = 0, // Resolved on save if product exists in Product Master
                ProductName = pName,
                Company = company,
                CategoryName = category,
                PackSize = packSize,
                Unit = unit,
                HSN = TxtHSN.Text?.Trim(),
                PurchasePrice = purchasePrice,
                SellingPrice = sellingPrice,
                GST = gst,
                BatchNumber = batch,
                ExpiryDate = DpExpiry.SelectedDate,
                Quantity = qty,
                FreeQuantity = freeQty,
                Amount = itemAmount
            };

            _items.Add(item);
            RecalculateSummary();

            // Reset product entry fields
            TxtProductName.Text = string.Empty;
            TxtCompany.Text = string.Empty;
            TxtPackSize.Text = string.Empty;
            TxtHSN.Text = string.Empty;
            TxtPurchasePrice.Text = string.Empty;
            TxtGST.Text = string.Empty;
            TxtBatch.Text = DatabaseHelper.GenerateNextBatchNumber();
            DpExpiry.SelectedDate = null;
            TxtQty.Text = string.Empty;
            TxtFreeQty.Text = string.Empty;

            TxtProductName.Focus();
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewOnly) return;
            if (sender is Button btn && btn.DataContext is PurchaseItem item)
            {
                _items.Remove(item);
                // reindex
                int idx = 1;
                foreach (var it in _items) it.PurchaseItemId = idx++;
                RecalculateSummary();
            }
        }

        #endregion

        #region Save Purchase & Validation

        private void BtnSavePurchase_Click(object sender, RoutedEventArgs e)
        {
            // 1. Supplier Name Validation
            string supplierName = TxtSupplierName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(supplierName))
            {
                MessageBox.Show("Supplier name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSupplierName.Focus();
                return;
            }

            // 2. Purchase Date Validation
            if (!DpPurchaseDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Purchase date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpPurchaseDate.Focus();
                return;
            }

            // 3. Supplier Invoice Number Validation & Uniqueness check
            string invoiceNo = TxtSupplierInvoice.Text?.Trim();
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                MessageBox.Show("Supplier invoice number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSupplierInvoice.Focus();
                return;
            }

            if (DatabaseHelper.IsSupplierInvoiceNoExists(invoiceNo, _viewPurchaseId))
            {
                MessageBox.Show($"Supplier Invoice Number '{invoiceNo}' already exists in database. Please enter a unique Supplier Invoice Number.", "Duplicate Invoice Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSupplierInvoice.Focus();
                return;
            }

            // 4. Products Validation
            if (_items.Count == 0)
            {
                MessageBox.Show("Please add at least one product.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtProductName.Focus();
                return;
            }

            // 5. Payment Mode Validation
            string paymentMode = (CmbPaymentMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            string paymentRef = TxtPaymentRef.Text?.Trim();
            if ((paymentMode.Contains("UPI") || paymentMode.Contains("Online")) && string.IsNullOrWhiteSpace(paymentRef))
            {
                MessageBox.Show("Transaction reference is required for UPI / Online payments.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPaymentRef.Focus();
                return;
            }

            // Calculations verification
            decimal subtotal = _items.Sum(i => i.Quantity * i.PurchasePrice);
            decimal discount = 0m;
            decimal.TryParse(TxtDiscount.Text?.Trim(), out discount);
            discount = Math.Max(0m, discount);
            decimal taxableAmount = Math.Max(0m, subtotal - discount);

            decimal totalGst = 0m;
            foreach (var item in _items)
            {
                decimal itemBasic = item.Quantity * item.PurchasePrice;
                decimal itemDiscountShare = subtotal > 0 ? (itemBasic / subtotal) * discount : 0m;
                decimal itemTaxable = Math.Max(0m, itemBasic - itemDiscountShare);
                decimal itemGst = Math.Round(itemTaxable * (item.GST / 100m), 2, MidpointRounding.AwayFromZero);
                totalGst += itemGst;
            }

            decimal rawGrandTotal = taxableAmount + totalGst;
            decimal grandTotal = Math.Round(rawGrandTotal, MidpointRounding.AwayFromZero);
            decimal roundOff = grandTotal - rawGrandTotal;

            // Paid amount validation
            decimal paidAmount = 0m;
            if (paymentMode.Contains("Credit") || paymentMode.Contains("Udhar"))
            {
                paidAmount = 0m;
            }
            else if (!string.IsNullOrWhiteSpace(TxtPaidAmount.Text?.Trim()))
            {
                if (!decimal.TryParse(TxtPaidAmount.Text.Trim(), out paidAmount) || paidAmount < 0)
                {
                    MessageBox.Show("Amount paid cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtPaidAmount.Focus();
                    return;
                }
            }

            if (paidAmount > grandTotal)
            {
                MessageBox.Show("Amount paid cannot exceed the grand total.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPaidAmount.Focus();
                return;
            }

            decimal payableAmount = Math.Max(0m, grandTotal - paidAmount);

            // Construct Purchase Record
            string purchaseNo = DatabaseHelper.GenerateNextPurchaseNo();
            var purchase = new Purchase
            {
                PurchaseNumber = purchaseNo,
                SupplierId = 0, // Saved/resolved in DatabaseHelper by Name
                SupplierName = supplierName,
                SupplierInvoiceNumber = invoiceNo,
                PaperBillNumber = string.Empty,
                PurchaseDate = DpPurchaseDate.SelectedDate.Value,
                SubTotal = subtotal,
                Discount = discount,
                TaxableAmount = taxableAmount,
                GSTAmount = totalGst,
                RoundOff = roundOff,
                GrandTotal = grandTotal,
                PaidAmount = paidAmount,
                PayableAmount = payableAmount,
                PaymentMethod = paymentMode,
                PaymentReference = paymentRef
            };

            // Prevent duplicate clicks & show saving status
            BtnSavePurchase.IsEnabled = false;
            TxtSavingStatus.Text = "Saving purchase...";

            try
            {
                int savedId = DatabaseHelper.SavePurchase(purchase, _items.ToList());
                MessageBox.Show($"Purchase saved successfully.\nPurchase Number: {purchaseNo}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving purchase: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSavePurchase.IsEnabled = true;
                TxtSavingStatus.Text = string.Empty;
            }
        }

        #endregion

        #region Load Purchase (View Mode)

        private void LoadPurchase(int purchaseId)
        {
            var p = DatabaseHelper.GetPurchaseById(purchaseId);
            if (p == null) return;

            TxtSupplierName.Text = p.SupplierName;
            TxtSupplierInvoice.Text = p.SupplierInvoiceNumber;
            DpPurchaseDate.SelectedDate = p.PurchaseDate;

            // Set payment mode
            foreach (ComboBoxItem item in CmbPaymentMode.Items)
            {
                if (item.Content?.ToString() == p.PaymentMethod)
                {
                    CmbPaymentMode.SelectedItem = item;
                    break;
                }
            }

            TxtPaymentRef.Text = p.PaymentReference;
            TxtDiscount.Text = p.Discount.ToString("N2");
            TxtPaidAmount.Text = p.PaidAmount.ToString("N2");

            var items = DatabaseHelper.GetPurchaseItems(purchaseId);
            _items.Clear();
            int idx = 1;
            foreach (var it in items)
            {
                it.PurchaseItemId = idx++;
                _items.Add(it);
            }

            RecalculateSummary();
        }

        #endregion

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
