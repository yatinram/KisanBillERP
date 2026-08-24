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
    public partial class PaymentReceiptWindow : Window
    {
        private readonly ObservableCollection<PaymentReceiptAllocation> _allocations = new ObservableCollection<PaymentReceiptAllocation>();
        private Farmer _selectedFarmer = null;
        private List<Farmer> _allFarmers = new List<Farmer>();
        private ObservableCollection<Farmer> _filteredFarmers = new ObservableCollection<Farmer>();
        private bool _isViewOnly = false;
        private decimal _openingBalance = 0m;

        public PaymentReceiptWindow(int receiptId = 0, bool viewOnly = false)
        {
            try
            {
                InitializeComponent();
                GridAllocations.ItemsSource = _allocations;
                _isViewOnly = viewOnly;

                if (DpReceiptDate != null) DpReceiptDate.SelectedDate = DateTime.Now;

                // load farmers for inline combo search
                try
                {
                    _allFarmers = DatabaseHelper.GetAllFarmers();
                    _filteredFarmers = new ObservableCollection<Farmer>(_allFarmers);
                    CmbFarmerSelect.ItemsSource = _filteredFarmers;
                }
                catch { }
                if (receiptId > 0)
                {
                    LoadPaymentReceipt(receiptId);
                    if (viewOnly)
                    {
                        EnableViewOnlyMode();
                    }
                }
            }
            catch (Exception ex)
            {
                KrushiBillERP.Data.Logger.Log(ex);
                MessageBox.Show($"Failed to open Payment Receipt window:\n{ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // prevent unhandled exception from crashing the app
            }
        }

        private void EnableViewOnlyMode()
        {
            TxtHeaderTitle.Text = "View Payment Receipt";
            TxtHeaderSubtitle.Text = "Payment receipt transaction reference";
            Title = "View Payment Receipt";

            // hide/disable inline farmer selector in view-only mode
            if (CmbFarmerSelect != null) CmbFarmerSelect.IsEnabled = false;
            if (TxtFarmerSearch != null) TxtFarmerSearch.IsEnabled = false;
            BtnAutoAllocate.Visibility = Visibility.Collapsed;
            BtnSave.Visibility = Visibility.Collapsed;

            DpReceiptDate.IsEnabled = false;
            TxtReceivedAmount.IsReadOnly = true;
            RbCash.IsEnabled = false;
            RbUPI.IsEnabled = false;
            RbCheque.IsEnabled = false;
            TxtTransRef.IsReadOnly = true;
            TxtChequeNo.IsReadOnly = true;
            DpChequeDate.IsEnabled = false;
            TxtBankName.IsReadOnly = true;
            TxtNotes.IsReadOnly = true;

            GridAllocations.IsReadOnly = true;
        }

        private void BtnSelectFarmer_Click(object sender, RoutedEventArgs e)
        {
            // Retained for compatibility: some views may still reference BtnSelectFarmer.Click.
            // This preserves behavior while allowing other code to safely call the handler.
            if (_isViewOnly) return;
            try
            {
                var selectWin = new FarmerSelectWindow();
                selectWin.Owner = this;
                if (selectWin.ShowDialog() == true && selectWin.SelectedFarmer != null)
                {
                    LoadFarmer(selectWin.SelectedFarmer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting customer/farmer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadFarmer(Farmer farmer)
        {
            try
            {
                _selectedFarmer = farmer;
                if (CmbFarmerSelect != null) { CmbFarmerSelect.Text = farmer.FarmerName; CmbFarmerSelect.SelectedItem = farmer; }
                if (TxtFarmerSearch != null) { TxtFarmerSearch.Text = string.Empty; if (HintFarmer != null) HintFarmer.Visibility = Visibility.Visible; }
                if (TxtMobile != null) TxtMobile.Text = farmer.MobileNumber;
                if (TxtVillage != null) TxtVillage.Text = farmer.VillageName;

                // Load live outstanding balance
                try
                {
                    _openingBalance = DatabaseHelper.GetFarmerOutstandingBalance(farmer.FarmerId);
                }
                catch (Exception dbEx)
                {
                    _openingBalance = 0m;
                    KrushiBillERP.Data.Logger.Log(dbEx);
                    MessageBox.Show($"Error fetching outstanding balance:\n{dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                if (TxtOpeningBalance != null) TxtOpeningBalance.Text = $"₹ {_openingBalance:N2}";

                // Load outstanding invoices
                try
                {
                    var invoices = DatabaseHelper.GetOutstandingInvoicesForFarmer(farmer.FarmerId);
                    _allocations.Clear();
                    foreach (var inv in invoices)
                    {
                        _allocations.Add(inv);
                    }
                }
                catch (Exception dbEx)
                {
                    _allocations.Clear();
                    KrushiBillERP.Data.Logger.Log(dbEx);
                    MessageBox.Show($"Error loading outstanding invoices:\n{dbEx.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (BorderAllocEmpty != null)
                {
                    BorderAllocEmpty.Visibility = _allocations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    if (_allocations.Count == 0 && TxtAllocEmpty != null)
                    {
                        TxtAllocEmpty.Text = "No outstanding invoices are available for payment allocation.";
                    }
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading farmer details:\n{ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtReceivedAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isViewOnly) return;
            UpdateSummary();
        }

        private void SetFarmerPopupVisible(bool visible)
        {
            if (PopupFarmerResults != null)
            {
                PopupFarmerResults.IsOpen = visible;
                PopupFarmerResults.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsFarmerPopupVisible()
        {
            return PopupFarmerResults != null && (PopupFarmerResults.IsOpen || PopupFarmerResults.Visibility == Visibility.Visible);
        }

        private void TxtFarmerSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape) return;

            try
            {
                var txt = (TxtFarmerSearch.Text ?? "").Trim();
                HintFarmer.Visibility = string.IsNullOrEmpty(txt) ? Visibility.Visible : Visibility.Collapsed;

                if (txt.Length >= 1)
                {
                    var matches = DatabaseHelper.SearchFarmersForPayment(txt);
                    ListFarmerResults.ItemsSource = matches;
                    SetFarmerPopupVisible(matches.Count > 0);
                }
                else
                {
                    SetFarmerPopupVisible(false);
                }
            }
            catch { }
        }

        private void TxtFarmerSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                SetFarmerPopupVisible(false);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Down && IsFarmerPopupVisible() && ListFarmerResults.Items.Count > 0)
            {
                ListFarmerResults.SelectedIndex = 0;
                var item = (ListBoxItem)ListFarmerResults.ItemContainerGenerator.ContainerFromIndex(0);
                if (item != null) item.Focus();
                else ListFarmerResults.Focus();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && IsFarmerPopupVisible() && ListFarmerResults.Items.Count > 0)
            {
                var f = ListFarmerResults.SelectedItem as Farmer ?? ListFarmerResults.Items[0] as Farmer;
                if (f != null) { LoadFarmer(f); TxtFarmerSearch.Text = string.Empty; HintFarmer.Visibility = Visibility.Visible; SetFarmerPopupVisible(false); }
                e.Handled = true;
            }
        }

        private void ListFarmerResults_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                SetFarmerPopupVisible(false);
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Up && ListFarmerResults.SelectedIndex <= 0)
            {
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Enter && ListFarmerResults.SelectedItem is Farmer f)
            {
                LoadFarmer(f);
                TxtFarmerSearch.Text = string.Empty;
                HintFarmer.Visibility = Visibility.Visible;
                SetFarmerPopupVisible(false);
                e.Handled = true;
            }
        }

        private void ListFarmerResults_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ListFarmerResults.SelectedItem is Farmer f)
            {
                LoadFarmer(f);
                TxtFarmerSearch.Text = string.Empty;
                HintFarmer.Visibility = Visibility.Visible;
                SetFarmerPopupVisible(false);
            }
        }

        private void CmbFarmerSelect_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Legacy - kept for backward compatibility
        }

        private void CmbFarmerSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFarmerSelect.SelectedItem is Farmer f)
            {
                LoadFarmer(f);
            }
        }

        private void PaymentMode_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelTransRef == null || PanelCheque == null) return;

            bool isUPI = RbUPI?.IsChecked == true;
            bool isCheque = RbCheque?.IsChecked == true;

            PanelTransRef.Visibility = isUPI ? Visibility.Visible : Visibility.Collapsed;
            PanelCheque.Visibility = isCheque ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAutoAllocate_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewOnly || _allocations.Count == 0) return;

            decimal.TryParse(TxtReceivedAmount.Text?.Trim(), out decimal receivedAmount);
            if (receivedAmount <= 0)
            {
                MessageBox.Show("Please enter a valid received amount first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtReceivedAmount.Focus();
                return;
            }

            decimal remaining = receivedAmount;
            foreach (var alloc in _allocations)
            {
                decimal apply = Math.Min(remaining, alloc.InvoiceOutstanding);
                alloc.AllocatedAmount = apply;
                remaining -= apply;
            }

            // Force DataGrid refresh
            GridAllocations.Items.Refresh();
            UpdateSummary();
        }

        private void Allocation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isViewOnly) return;
            if (sender is TextBox tb && tb.DataContext is PaymentReceiptAllocation alloc)
            {
                if (decimal.TryParse(tb.Text?.Trim(), out decimal val))
                {
                    if (val < 0)
                    {
                        MessageBox.Show("Allocation amount cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        alloc.AllocatedAmount = 0m;
                        tb.Text = "0";
                    }
                    else if (val > alloc.InvoiceOutstanding)
                    {
                        MessageBox.Show($"Allocation cannot exceed the outstanding amount (₹{alloc.InvoiceOutstanding:N2}) for this invoice.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        alloc.AllocatedAmount = alloc.InvoiceOutstanding;
                        tb.Text = alloc.InvoiceOutstanding.ToString("F2");
                    }
                    else
                    {
                        alloc.AllocatedAmount = val;
                    }
                }
                else if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    alloc.AllocatedAmount = 0m;
                }
                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            if (TxtSummaryOpening == null || TxtSummaryReceived == null || TxtSummaryClosing == null || TxtTotalAllocated == null) return;

            decimal.TryParse(TxtReceivedAmount.Text?.Trim(), out decimal received);

            decimal totalAllocated = _allocations.Sum(a => a.AllocatedAmount);
            TxtTotalAllocated.Text = $"Total Allocated: ₹ {totalAllocated:N2}";

            decimal closing = Math.Max(0m, _openingBalance - received);

            TxtSummaryOpening.Text = $"₹ {_openingBalance:N2}";
            TxtSummaryReceived.Text = $"₹ {received:N2}";
            TxtSummaryClosing.Text = $"₹ {closing:N2}";

            if (TxtAllocStatus != null)
            {
                if (received > 0 && Math.Abs(totalAllocated - received) > 0.01m)
                {
                    TxtAllocStatus.Text = totalAllocated < received
                        ? $"⚠️ Unallocated: ₹ {(received - totalAllocated):N2}"
                        : $"⚠️ Over-allocated: ₹ {(totalAllocated - received):N2}";
                }
                else
                {
                    TxtAllocStatus.Text = string.Empty;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFarmer == null)
            {
                MessageBox.Show("Please select a customer or farmer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (CmbFarmerSelect != null) CmbFarmerSelect.Focus();
                return;
            }

            if (!DpReceiptDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Receipt date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpReceiptDate.Focus();
                return;
            }

            if (!decimal.TryParse(TxtReceivedAmount.Text?.Trim(), out decimal receivedAmount) || receivedAmount <= 0)
            {
                MessageBox.Show("Received amount is required and must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtReceivedAmount.Focus();
                return;
            }

            if (receivedAmount > _openingBalance)
            {
                MessageBox.Show($"Received amount (₹{receivedAmount:N2}) cannot exceed the current outstanding balance (₹{_openingBalance:N2}).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtReceivedAmount.Focus();
                return;
            }

            // Payment Mode validations
            string mode = "Cash";
            if (RbUPI.IsChecked == true) mode = "UPI / Online";
            else if (RbCheque.IsChecked == true) mode = "Cheque";

            if (mode == "UPI / Online" && string.IsNullOrWhiteSpace(TxtTransRef.Text))
            {
                MessageBox.Show("Transaction reference is required for UPI / Online payments.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTransRef.Focus();
                return;
            }

            if (mode == "Cheque")
            {
                if (string.IsNullOrWhiteSpace(TxtChequeNo.Text))
                {
                    MessageBox.Show("Cheque number is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtChequeNo.Focus();
                    return;
                }
                if (!DpChequeDate.SelectedDate.HasValue)
                {
                    MessageBox.Show("Cheque date is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    DpChequeDate.Focus();
                    return;
                }
            }

            // Allocation validation
            var activeAllocations = _allocations.Where(a => a.AllocatedAmount > 0).ToList();
            decimal totalAllocated = activeAllocations.Sum(a => a.AllocatedAmount);

            if (Math.Abs(totalAllocated - receivedAmount) > 0.01m)
            {
                MessageBox.Show($"Payment allocation (₹{totalAllocated:N2}) must equal the received amount (₹{receivedAmount:N2}).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Construct PaymentReceipt
            string receiptNo = DatabaseHelper.GenerateNextReceiptNo();
            var receipt = new PaymentReceipt
            {
                ReceiptNumber = receiptNo,
                FarmerId = _selectedFarmer.FarmerId,
                FarmerName = _selectedFarmer.FarmerName,
                MobileNumber = _selectedFarmer.MobileNumber,
                VillageName = _selectedFarmer.VillageName,
                ReceiptDate = DpReceiptDate.SelectedDate.Value,
                OpeningBalance = _openingBalance,
                ReceivedAmount = receivedAmount,
                ClosingBalance = _openingBalance - receivedAmount,
                PaymentMode = mode,
                TransactionReference = TxtTransRef.Text?.Trim(),
                ChequeNumber = TxtChequeNo.Text?.Trim(),
                ChequeDate = DpChequeDate.SelectedDate?.ToString("yyyy-MM-dd"),
                BankName = TxtBankName.Text?.Trim(),
                Notes = TxtNotes.Text?.Trim()
            };

            BtnSave.IsEnabled = false;
            TxtSavingStatus.Text = "Saving payment receipt...";

            try
            {
                int savedId = DatabaseHelper.SavePaymentReceipt(receipt, activeAllocations);
                MessageBox.Show($"Payment receipt saved successfully.\nReceipt Number: {receiptNo}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving payment receipt: {ex.Message}", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnSave.IsEnabled = true;
                TxtSavingStatus.Text = string.Empty;
            }
        }

        private void LoadPaymentReceipt(int receiptId)
        {
            var r = DatabaseHelper.GetPaymentReceiptById(receiptId);
            if (r == null) return;

            // set farmer selection into editable combo
            var farmer = DatabaseHelper.GetFarmerById(r.FarmerId);
            if (farmer != null && CmbFarmerSelect != null)
            {
                _selectedFarmer = farmer;
                CmbFarmerSelect.Text = farmer.FarmerName;
                CmbFarmerSelect.SelectedItem = farmer;
            }
            TxtMobile.Text = r.MobileNumber;
            TxtVillage.Text = r.VillageName;
            DpReceiptDate.SelectedDate = r.ReceiptDate;

            _openingBalance = r.OpeningBalance;
            TxtOpeningBalance.Text = $"₹ {r.OpeningBalance:N2}";
            TxtReceivedAmount.Text = r.ReceivedAmount.ToString("F2");

            if (r.PaymentMode == "UPI / Online") RbUPI.IsChecked = true;
            else if (r.PaymentMode == "Cheque") RbCheque.IsChecked = true;
            else RbCash.IsChecked = true;

            TxtTransRef.Text = r.TransactionReference;
            TxtChequeNo.Text = r.ChequeNumber;
            if (DateTime.TryParse(r.ChequeDate, out DateTime cd)) DpChequeDate.SelectedDate = cd;
            TxtBankName.Text = r.BankName;
            TxtNotes.Text = r.Notes;

            var allocs = DatabaseHelper.GetPaymentReceiptAllocations(receiptId);
            _allocations.Clear();
            foreach (var a in allocs)
            {
                _allocations.Add(a);
            }

            if (BorderAllocEmpty != null)
            {
                BorderAllocEmpty.Visibility = Visibility.Collapsed;
            }

            UpdateSummary();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
