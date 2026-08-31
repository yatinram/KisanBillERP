using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class BillingView : Page
    {
        // Current invoice being edited (0 = new bill)
        private int _editingInvoiceId = 0;
        private readonly ObservableCollection<InvoiceItem> _items = new ObservableCollection<InvoiceItem>();
        private Farmer _selectedFarmer = null;
        private Product _selectedProductBatch = null;
        private bool _isUserManualAmountReceived = false;

        private static readonly SolidColorBrush BrushAccentGreen = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
        private static readonly SolidColorBrush BrushMutedText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

        public BillingView()
        {
            try
            {
                InitializeComponent();

                // Global Page Shortcuts (F2, F3, F4, F8, F9)
                this.Loaded += (s, e) =>
                {
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.PreviewKeyDown -= Window_PreviewKeyDown;
                        window.PreviewKeyDown += Window_PreviewKeyDown;
                    }
                };

                // Default Active Tab: ADD BILL
                SetActiveTab("add");

                // Initialize New Bill State
                InitNewBill();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Billing View Initialize Error: {ex.Message}\n\nDetails: {ex.InnerException?.Message ?? ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== KEYBOARD SHORTCUTS =====================

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewAddBill.Visibility != Visibility.Visible) return;

            if (e.Key == Key.F2)
            {
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                TxtProductSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F4)
            {
                TxtAmountReceived.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.F8 || (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control))
            {
                BtnSaveBill_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                BtnClearBill_Click(null, null);
                e.Handled = true;
            }
        }

        private void InitNewBill()
        {
            TxtInvoiceNoDisplay.Text = DatabaseHelper.GenerateNextInvoiceNo();
            TxtPaperBillNo.Text = string.Empty;
            // Ensure paper bill is editable in new bill mode
            if (TxtPaperBillNo != null)
            {
                TxtPaperBillNo.IsReadOnly = false;
                TxtPaperBillNo.Background = System.Windows.Media.Brushes.White;
            }
            DpBillDate.SelectedDate = DateTime.Today;

            _items.Clear();
            GridItems.ItemsSource = _items;

            _selectedFarmer = null;
            _selectedProductBatch = null;

            PanelSelectedFarmer.Visibility = Visibility.Collapsed;
            PopupFarmerResults.Visibility = Visibility.Collapsed;
            TxtFarmerSearch.Text = string.Empty;
            TxtFarmerSearchPlaceholder.Visibility = Visibility.Visible;

            if (CmbFarmerSelect != null)
            {
                RefreshFarmerComboBox();
                CmbFarmerSelect.SelectedItem = null;
                CmbFarmerSelect.Text = string.Empty;
            }

            PanelAddBar.Visibility = Visibility.Collapsed;
            PopupProductResults.Visibility = Visibility.Collapsed;
            TxtProductSearch.Text = string.Empty;
            TxtProductSearchPlaceholder.Visibility = Visibility.Visible;
            TxtQty.Text = "1";
            TxtRate.Text = string.Empty;

            TxtDiscount.Text = "0";
            RadioCash.IsChecked = true;
            PanelUpiRef.Visibility = Visibility.Collapsed;
            TxtUpiRef.Text = string.Empty;

            RecalculateTotals();
            TxtFarmerSearch.Focus();

            // Reset edit state and save button label
            _editingInvoiceId = 0;
            _isUserManualAmountReceived = false;
            if (BtnSaveBill != null) BtnSaveBill.Content = "✓ Save Bill";
            if (BtnChangeFarmer != null) BtnChangeFarmer.IsEnabled = true;
            // Show post-save notice in new bill mode
            if (PostSaveNotice != null) PostSaveNotice.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Load an existing invoice into the Add Bill section for in-place editing.
        /// </summary>
        public void LoadInvoiceForEdit(int invoiceId, bool lockFarmer = false)
        {
            try
            {
                var inv = DatabaseHelper.GetInvoiceById(invoiceId);
                if (inv == null)
                {
                    MessageBox.Show("Invoice record not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Header
                if (TxtInvoiceNoDisplay != null) TxtInvoiceNoDisplay.Text = inv.InvoiceNo ?? DatabaseHelper.GenerateNextInvoiceNo();
                if (TxtPaperBillNo != null) TxtPaperBillNo.Text = inv.PaperBillNo ?? string.Empty;
                if (DpBillDate != null) DpBillDate.SelectedDate = inv.InvoiceDate;

                // When loading for edit, lock the Paper Bill No field and change save button to Update
                _editingInvoiceId = invoiceId;
                if (TxtPaperBillNo != null)
                {
                    TxtPaperBillNo.IsReadOnly = true;
                    TxtPaperBillNo.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F1F5F9");
                }
                if (BtnSaveBill != null) BtnSaveBill.Content = "✓ Update Bill";
                // Hide post-save options when editing an existing bill
                if (PostSaveNotice != null) PostSaveNotice.Visibility = Visibility.Collapsed;

                // Farmer
                if (inv.FarmerId > 0)
                {
                    var f = DatabaseHelper.GetFarmerById(inv.FarmerId);
                    if (f != null) SelectFarmer(f);

                    // If requested, lock farmer so it cannot be changed while editing
                    if (lockFarmer)
                    {
                        try
                        {
                            if (TxtFarmerSearch != null)
                            {
                                TxtFarmerSearch.IsReadOnly = true;
                                TxtFarmerSearch.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#F1F5F9");
                                TxtFarmerSearch.Cursor = System.Windows.Input.Cursors.Arrow;
                            }

                            if (BtnSelectFarmer != null) BtnSelectFarmer.IsEnabled = false;
                            if (PopupFarmerResults != null) PopupFarmerResults.Visibility = Visibility.Collapsed;
                            if (BtnChangeFarmer != null) BtnChangeFarmer.IsEnabled = false;
                        }
                        catch { }
                    }
                    else
                    {
                        // Ensure change farmer is enabled for normal (non-locked) edits
                        if (BtnChangeFarmer != null) BtnChangeFarmer.IsEnabled = true;
                    }
                }
                else
                {
                    _selectedFarmer = null;
                    if (PanelSelectedFarmer != null) PanelSelectedFarmer.Visibility = Visibility.Collapsed;
                }

                // Payment fields
                if (TxtDiscount != null) TxtDiscount.Text = inv.Discount.ToString();
                if (TxtUpiRef != null) TxtUpiRef.Text = inv.PaymentReference ?? string.Empty;
                if (TxtAmountReceived != null) TxtAmountReceived.Text = inv.PaidAmount.ToString("F2");

                if (inv.PaymentMethod == "UPI / Online") RadioUpi.IsChecked = true;
                else if (inv.PaymentMethod == "Udhar") RadioUdhar.IsChecked = true;
                else RadioCash.IsChecked = true;

                // Items
                _items.Clear();
                var items = DatabaseHelper.GetInvoiceItems(invoiceId);
                foreach (var it in items)
                {
                    _items.Add(it);
                }

                RecalculateTotals();
                TxtProductSearch?.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading invoice for edit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== TOP TAB NAVIGATION =====================

        private void SetActiveTab(string tab)
        {
            if (tab == "add")
            {
                ViewAddBill.Visibility = Visibility.Visible;
                ViewBillHistory.Visibility = Visibility.Collapsed;
            }
            else
            {
                ViewAddBill.Visibility = Visibility.Collapsed;
                ViewBillHistory.Visibility = Visibility.Visible;

                if (FrameBillHistory.Content == null)
                {
                    FrameBillHistory.Navigate(new InvoicesView());
                }
            }
        }

        private void TabAddBill_Click(object sender, RoutedEventArgs e) => SetActiveTab("add");

        private void TabBillHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count > 0 || _selectedFarmer != null)
            {
                var result = MessageBox.Show("Changing tab will discard unsaved bill data. Continue?", "Unsaved Bill", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            SetActiveTab("history");
        }

        // ===================== STEP 1: FARMER SEARCH & SELECTION =====================

        private void BtnSelectFarmer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectWin = new FarmerSelectWindow();
                var parent = Window.GetWindow(this);
                if (parent != null) selectWin.Owner = parent;

                if (selectWin.ShowDialog() == true && selectWin.SelectedFarmer != null)
                {
                    SelectFarmer(selectWin.SelectedFarmer);
                    PopupFarmerResults.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting customer/farmer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<Farmer> _farmerComboList = new List<Farmer>();

        private void CmbFarmerSelect_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshFarmerComboBox();
                var tb = CmbFarmerSelect.Template.FindName("PART_EditableTextBox", CmbFarmerSelect) as TextBox;
                if (tb != null)
                {
                    tb.TextChanged -= CmbFarmerSelect_TextChanged;
                    tb.TextChanged += CmbFarmerSelect_TextChanged;
                }
            }
            catch { }
        }

        private void RefreshFarmerComboBox()
        {
            _farmerComboList = DatabaseHelper.SearchFarmersByNameOrMobile("");
            CmbFarmerSelect.ItemsSource = new ObservableCollection<Farmer>(_farmerComboList);
        }

        private void CmbFarmerSelect_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CmbFarmerSelect == null || CmbFarmerSelect.ItemsSource == null) return;
            string filterText = CmbFarmerSelect.Text?.Trim() ?? "";

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(CmbFarmerSelect.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    if (string.IsNullOrWhiteSpace(filterText)) return true;
                    if (item is Farmer f)
                    {
                        return (f.FarmerName != null && f.FarmerName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (f.MobileNumber != null && f.MobileNumber.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    return false;
                };
                view.Refresh();
                if (filterText.Length > 0 && !CmbFarmerSelect.IsDropDownOpen)
                {
                    CmbFarmerSelect.IsDropDownOpen = true;
                }
            }
        }

        private void CmbFarmerSelect_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter || e.Key == Key.Escape) return;
            string filterText = CmbFarmerSelect.Text?.Trim() ?? "";
            CmbFarmerSelect_TextChanged(null, null);
        }

        private void CmbFarmerSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbFarmerSelect.SelectedItem is Farmer f)
            {
                SelectFarmer(f);
                if (TxtPaperBillNo != null) TxtPaperBillNo.Focus();
            }
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

        private void TxtFarmerSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter || e.Key == Key.Escape) return;

            string search = TxtFarmerSearch.Text.Trim();
            TxtFarmerSearchPlaceholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            if (search.Length >= 1)
            {
                var matches = DatabaseHelper.SearchFarmersByNameOrMobile(search);
                ListFarmers.ItemsSource = matches;
                SetFarmerPopupVisible(matches.Count > 0);
            }
            else
            {
                SetFarmerPopupVisible(false);
            }
        }

        private void TxtFarmerSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SetFarmerPopupVisible(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && IsFarmerPopupVisible() && ListFarmers.Items.Count > 0)
            {
                ListFarmers.SelectedIndex = 0;
                var item = (ListBoxItem)ListFarmers.ItemContainerGenerator.ContainerFromIndex(0);
                if (item != null)
                {
                    item.Focus();
                }
                else
                {
                    ListFarmers.Focus();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && IsFarmerPopupVisible() && ListFarmers.Items.Count > 0)
            {
                var selected = ListFarmers.SelectedItem as Farmer ?? ListFarmers.Items[0] as Farmer;
                if (selected != null)
                {
                    SelectFarmer(selected);
                    SetFarmerPopupVisible(false);
                    TxtPaperBillNo.Focus();
                    e.Handled = true;
                }
            }
        }

        private void ListFarmers_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SetFarmerPopupVisible(false);
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && ListFarmers.SelectedIndex == 0)
            {
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ListFarmers.SelectedItem is Farmer f)
            {
                SelectFarmer(f);
                SetFarmerPopupVisible(false);
                TxtPaperBillNo.Focus();
                e.Handled = true;
            }
        }

        private void ListFarmers_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ListFarmers.SelectedItem is Farmer f)
            {
                SelectFarmer(f);
                SetFarmerPopupVisible(false);
                TxtPaperBillNo.Focus();
            }
        }

        private void SelectFarmer(Farmer f)
        {
            _selectedFarmer = f;
            TxtSelectedFarmerName.Text = f.FarmerName;
            TxtSelectedFarmerMobile.Text = string.IsNullOrEmpty(f.MobileNumber) ? "-" : f.MobileNumber;
            TxtSelectedFarmerVillage.Text = string.IsNullOrEmpty(f.VillageName) ? "-" : f.VillageName;

            var (balAmt, balType) = DatabaseHelper.GetFarmerAccountLedgerBalance(f.FarmerId);
            if (balType == "Jama")
            {
                TxtSelectedFarmerOutstanding.Text = $"₹ {balAmt:N2} Jama (Adv)";
                TxtSelectedFarmerOutstanding.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#16A34A"));
            }
            else if (balType == "Udhar")
            {
                TxtSelectedFarmerOutstanding.Text = $"₹ {balAmt:N2} Udhar";
                TxtSelectedFarmerOutstanding.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
            }
            else
            {
                TxtSelectedFarmerOutstanding.Text = "₹ 0.00 (Clear)";
                TxtSelectedFarmerOutstanding.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"));
            }

            PanelSelectedFarmer.Visibility = Visibility.Visible;

            if (TxtFarmerSearch != null)
            {
                TxtFarmerSearch.Text = string.Empty;
                if (TxtFarmerSearchPlaceholder != null) TxtFarmerSearchPlaceholder.Visibility = Visibility.Visible;
            }
            SetFarmerPopupVisible(false);

            TxtPaperBillNo.Focus();
        }

        private void BtnChangeFarmer_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count > 0)
            {
                var res = MessageBox.Show("Changing the farmer will clear current bill items. Do you want to continue?", "Change Farmer", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
                _items.Clear();
                RecalculateTotals();
            }

            _selectedFarmer = null;
            PanelSelectedFarmer.Visibility = Visibility.Collapsed;
            TxtFarmerSearch.Text = string.Empty;
            TxtFarmerSearchPlaceholder.Visibility = Visibility.Visible;
            TxtFarmerSearch.Focus();
        }

        // ===================== STEP 2: BILL DETAILS ENTER FLOW =====================

        private void TxtPaperBillNo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DpBillDate.Focus();
                e.Handled = true;
            }
        }

        private void DpBillDate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TxtProductSearch.Focus();
                e.Handled = true;
            }
        }

        // ===================== STEP 3: PRODUCT SEARCH & SELECTION =====================

        private void SetProductPopupVisible(bool visible)
        {
            if (PopupProductResults != null)
            {
                PopupProductResults.IsOpen = visible;
                PopupProductResults.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsProductPopupVisible()
        {
            return PopupProductResults != null && (PopupProductResults.IsOpen || PopupProductResults.Visibility == Visibility.Visible);
        }

        private void TxtProductSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter || e.Key == Key.Escape) return;

            string search = TxtProductSearch.Text.Trim();
            TxtProductSearchPlaceholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            if (search.Length >= 1)
            {
                var matches = DatabaseHelper.GetProductsInStock(search);
                ListProducts.ItemsSource = matches;
                SetProductPopupVisible(matches.Count > 0);
            }
            else
            {
                SetProductPopupVisible(false);
            }
        }

        private void TxtProductSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SetProductPopupVisible(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && IsProductPopupVisible() && ListProducts.Items.Count > 0)
            {
                ListProducts.SelectedIndex = 0;
                var item = (ListBoxItem)ListProducts.ItemContainerGenerator.ContainerFromIndex(0);
                if (item != null) item.Focus();
                else ListProducts.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && IsProductPopupVisible() && ListProducts.Items.Count > 0)
            {
                SelectProductBatch(ListProducts.SelectedItem as Product ?? ListProducts.Items[0] as Product);
                SetProductPopupVisible(false);
                TxtQty.Focus();
                TxtQty.SelectAll();
                e.Handled = true;
            }
        }

        private void ListProducts_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SetProductPopupVisible(false);
                TxtProductSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && ListProducts.SelectedIndex <= 0)
            {
                TxtProductSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ListProducts.SelectedItem is Product p)
            {
                SelectProductBatch(p);
                SetProductPopupVisible(false);
                TxtQty.Focus();
                TxtQty.SelectAll();
                e.Handled = true;
            }
        }

        private void ListProducts_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ListProducts.SelectedItem is Product p)
            {
                SelectProductBatch(p);
                SetProductPopupVisible(false);
                TxtQty.Focus();
                TxtQty.SelectAll();
            }
        }

        private void GridProductSuggestions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ListProducts_PreviewKeyDown(sender, e);
        }

        private void GridProductSuggestions_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ListProducts_PreviewMouseLeftButtonUp(sender, e);
        }

        private void SelectProductBatch(Product p)
        {
            if (p == null) return;
            _selectedProductBatch = p;
            TxtRate.Text = p.SalePrice.ToString("N2");

            TxtAddProdName.Text = $"{p.Name} ({p.Company})";
            TxtAddBatch.Text = p.BatchNo;
            TxtAddExp.Text = p.ExpiryDisplay;
            TxtAddStock.Text = $"{p.StockQty} {p.Unit}";
            PanelAddBar.Visibility = Visibility.Visible;

            if (TxtProductSearch != null)
            {
                TxtProductSearch.Text = string.Empty;
                if (TxtProductSearchPlaceholder != null) TxtProductSearchPlaceholder.Visibility = Visibility.Visible;
            }
            SetProductPopupVisible(false);
        }

        private void TxtQty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnAddItem_Click(null, null);
                e.Handled = true;
            }
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProductBatch == null)
            {
                MessageBox.Show("Please select a product first.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedProductBatch.ExpiryDate.HasValue && _selectedProductBatch.ExpiryDate.Value.Date < DateTime.Today)
            {
                MessageBox.Show($"This product batch has expired ({_selectedProductBatch.ExpiryDisplay}) and cannot be sold.", "Expired Product", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(TxtQty.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Quantity must be greater than zero.", "Invalid Qty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int alreadyAdded = _items.Where(i => i.ProductId == _selectedProductBatch.Id).Sum(i => i.Qty);
            int availableStock = _selectedProductBatch.StockQty - alreadyAdded;

            if (availableStock <= 0)
            {
                MessageBox.Show("This product is out of stock.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (qty > availableStock)
            {
                MessageBox.Show($"Insufficient stock. Available quantity: {availableStock}.", "Insufficient Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(TxtRate.Text, out decimal rate);
            if (rate <= 0) rate = _selectedProductBatch.SalePrice;

            decimal amount = Math.Round(qty * rate, 2);

            _items.Add(new InvoiceItem
            {
                ProductId = _selectedProductBatch.Id,
                ProductName = _selectedProductBatch.Name,
                Company = _selectedProductBatch.Company,
                BatchNo = _selectedProductBatch.BatchNo,
                ExpiryDate = _selectedProductBatch.ExpiryDate,
                Unit = _selectedProductBatch.Unit,
                Qty = qty,
                Rate = rate,
                GstPercent = _selectedProductBatch.GstPercent,
                Amount = amount,
                HSN = _selectedProductBatch.HSN
            });

            RecalculateTotals();

            // Reset Product Search for fast multi-product entry
            _selectedProductBatch = null;
            TxtProductSearch.Text = string.Empty;
            TxtProductSearchPlaceholder.Visibility = Visibility.Visible;
            PanelAddBar.Visibility = Visibility.Collapsed;
            TxtQty.Text = "1";
            TxtRate.Text = string.Empty;
            TxtProductSearch.Focus();
        }

        // ===================== STEP 4: BILL ITEMS QTY STEPPER & REMOVE =====================

        private void BtnQtyPlus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int productId)
            {
                var item = _items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    var p = DatabaseHelper.GetProducts().FirstOrDefault(x => x.Id == productId);
                    if (p != null && item.Qty + 1 > p.StockQty)
                    {
                        MessageBox.Show($"Insufficient stock. Available quantity: {p.StockQty}.", "Stock Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    item.Qty++;
                    item.Amount = Math.Round(item.Qty * item.Rate, 2);
                    GridItems.Items.Refresh();
                    RecalculateTotals();
                }
            }
        }

        private void BtnQtyMinus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int productId)
            {
                var item = _items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    if (item.Qty > 1)
                    {
                        item.Qty--;
                        item.Amount = Math.Round(item.Qty * item.Rate, 2);
                        GridItems.Items.Refresh();
                        RecalculateTotals();
                    }
                }
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int productId)
            {
                var item = _items.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    _items.Remove(item);
                    RecalculateTotals();
                }
            }
        }

        // ===================== STEP 5 & 6: FINANCIAL CALCULATIONS =====================

        private void RecalculateTotals()
        {
            if (TxtSubTotal == null || TxtTaxable == null || TxtGstTotal == null || TxtGrandTotal == null || TxtSummaryPayMode == null || RadioUpi == null || RadioUdhar == null) return;
            if (_items == null) return;

            decimal subTotal = _items.Sum(i => i.Qty * i.Rate);

            decimal discountPercent = 0m;
            if (TxtDiscount != null && decimal.TryParse(TxtDiscount.Text?.Trim(), out decimal dp))
            {
                discountPercent = Math.Max(0m, Math.Min(100m, dp));
            }

            decimal discountAmount = Math.Round(subTotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
            decimal taxable = Math.Max(0, subTotal - discountAmount);

            decimal gstTotal = 0m;
            foreach (var item in _items)
            {
                decimal itemBasic = item.Qty * item.Rate;
                decimal itemDiscShare = subTotal > 0 ? (itemBasic / subTotal) * discountAmount : 0m;
                decimal itemTaxable = Math.Max(0, itemBasic - itemDiscShare);
                decimal itemGst = Math.Round(itemTaxable * (item.GstPercent / 100m), 2, MidpointRounding.AwayFromZero);
                gstTotal += itemGst;
            }

            decimal grandTotal = Math.Round(taxable + gstTotal, 2);

            TxtSubTotal.Text = $"₹ {subTotal:N2}";
            TxtTaxable.Text = $"₹ {taxable:N2}";
            TxtGstTotal.Text = $"₹ {gstTotal:N2}";
            TxtGrandTotal.Text = $"₹ {grandTotal:N2}";

            string payMode = "Cash";
            if (RadioUpi.IsChecked == true) payMode = "UPI / Online";
            else if (RadioUdhar.IsChecked == true) payMode = "Udhar";
            TxtSummaryPayMode.Text = payMode;

            if (RadioUdhar.IsChecked == true)
            {
                if (PanelAmountReceived != null) PanelAmountReceived.Visibility = Visibility.Collapsed;
                if (PanelUpiRef != null) PanelUpiRef.Visibility = Visibility.Collapsed;
                if (TxtAmountReceived != null) TxtAmountReceived.Text = "0.00";
            }
            else if (RadioUpi.IsChecked == true)
            {
                if (PanelAmountReceived != null) PanelAmountReceived.Visibility = Visibility.Visible;
                if (PanelUpiRef != null) PanelUpiRef.Visibility = Visibility.Visible;
                if (TxtAmountReceived != null && (!_isUserManualAmountReceived || string.IsNullOrWhiteSpace(TxtAmountReceived.Text) || TxtAmountReceived.Text == "0.00"))
                {
                    TxtAmountReceived.Text = grandTotal.ToString("F2");
                }
            }
            else // Cash
            {
                if (PanelAmountReceived != null) PanelAmountReceived.Visibility = Visibility.Visible;
                if (PanelUpiRef != null) PanelUpiRef.Visibility = Visibility.Collapsed;
                if (TxtAmountReceived != null && (!_isUserManualAmountReceived || string.IsNullOrWhiteSpace(TxtAmountReceived.Text) || TxtAmountReceived.Text == "0.00"))
                {
                    TxtAmountReceived.Text = grandTotal.ToString("F2");
                }
            }

            RecalculateOutstanding(grandTotal);
        }

        private void RecalculateOutstanding(decimal grandTotal)
        {
            if (TxtAmountReceived == null || TxtBillOutstanding == null || TxtSummaryReceived == null || TxtSummaryOutstanding == null) return;

            decimal.TryParse(TxtAmountReceived.Text, out decimal received);
            if (received < 0) received = 0;

            decimal outstanding = Math.Max(0, grandTotal - received);
            TxtBillOutstanding.Text = $"{outstanding:N2}";
            TxtSummaryReceived.Text = $"₹ {received:N2}";
            TxtSummaryOutstanding.Text = $"₹ {outstanding:N2}";
        }

        private void TxtDiscount_KeyUp(object sender, KeyEventArgs e)
        {
            RecalculateTotals();
        }

        private void TxtDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateTotals();
        }

        private void TxtAmountReceived_KeyUp(object sender, KeyEventArgs e)
        {
            _isUserManualAmountReceived = true;
            decimal.TryParse(TxtGrandTotal.Text.Replace("₹", "").Trim(), out decimal grand);
            RecalculateOutstanding(grand);
        }

        private void TxtAmountReceived_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSaveBill.Focus();
                e.Handled = true;
            }
        }

        private void PayMode_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelUpiRef == null) return;

            if (RadioUpi.IsChecked == true)
            {
                PanelUpiRef.Visibility = Visibility.Visible;
            }
            else
            {
                PanelUpiRef.Visibility = Visibility.Collapsed;
            }

            RecalculateTotals();
        }

        // ===================== SAVE & CLEAR BILL =====================

        private void BtnSaveBill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedFarmer == null)
                {
                    MessageBox.Show("Please select a farmer first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtFarmerSearch.Focus();
                    return;
                }

                if (_items.Count == 0)
                {
                    MessageBox.Show("Please add at least one product to the bill.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtProductSearch.Focus();
                    return;
                }

                string paperBillNo = TxtPaperBillNo.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(paperBillNo))
                {
                    MessageBox.Show("Paper Bill No. is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtPaperBillNo.Focus();
                    return;
                }

                if (DatabaseHelper.IsPaperBillNoExists(paperBillNo, _editingInvoiceId))
                {
                    MessageBox.Show($"Paper Bill No. '{paperBillNo}' already exists in database. Please enter a unique Paper Bill No.", "Duplicate Paper Bill No", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtPaperBillNo.Focus();
                    return;
                }

                decimal subTotal = _items.Sum(i => i.Qty * i.Rate);

                decimal discountPercent = 0m;
                if (TxtDiscount != null && decimal.TryParse(TxtDiscount.Text?.Trim(), out decimal dp))
                {
                    discountPercent = Math.Max(0m, Math.Min(100m, dp));
                }
                decimal discountAmount = Math.Round(subTotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);

                decimal taxable = Math.Max(0, subTotal - discountAmount);
                decimal gstTotal = 0m;
                foreach (var item in _items)
                {
                    decimal itemBasic = item.Qty * item.Rate;
                    decimal itemDiscShare = subTotal > 0 ? (itemBasic / subTotal) * discountAmount : 0m;
                    decimal itemTaxable = Math.Max(0, itemBasic - itemDiscShare);
                    decimal itemGst = Math.Round(itemTaxable * (item.GstPercent / 100m), 2, MidpointRounding.AwayFromZero);
                    gstTotal += itemGst;
                }
                decimal grandTotal = Math.Round(taxable + gstTotal, 2);

                string payMode = "Cash";
                if (RadioUpi.IsChecked == true) payMode = "UPI";
                else if (RadioUdhar.IsChecked == true) payMode = "Udhar";

                decimal paidAmount = 0m;
                if (payMode == "Udhar")
                {
                    paidAmount = 0m;
                }
                else
                {
                    decimal.TryParse(TxtAmountReceived.Text, out paidAmount);
                    if (paidAmount > grandTotal)
                    {
                        MessageBox.Show("Amount received cannot exceed the grand total.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (payMode == "UPI" && string.IsNullOrWhiteSpace(TxtUpiRef.Text))
                {
                    MessageBox.Show("Transaction reference / UTR is required for UPI payment.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtUpiRef.Focus();
                    return;
                }

                decimal payableAmount = Math.Max(0, grandTotal - paidAmount);

                // ── Pre-Save Confirmation Dialog (Only save if user clicks OK) ──
                string confirmTitle = _editingInvoiceId > 0 ? "Confirm Update Bill" : "Confirm Save Bill";
                string confirmMessage = _editingInvoiceId > 0 ?
                    $"Are you sure you want to update Bill '{TxtInvoiceNoDisplay.Text.Trim()}'?\n\n👤 Farmer: {_selectedFarmer.FarmerName}\n📋 Paper Bill No: {paperBillNo}\n💰 Grand Total: ₹{grandTotal:N2}\n💳 Payment Mode: {payMode}\n\nClick OK to confirm and update." :
                    $"Are you sure you want to save Bill '{TxtInvoiceNoDisplay.Text.Trim()}'?\n\n👤 Farmer: {_selectedFarmer.FarmerName}\n📋 Paper Bill No: {paperBillNo}\n💰 Grand Total: ₹{grandTotal:N2}\n💳 Payment Mode: {payMode}\n\nClick OK to confirm and save.";

                var confirmResult = MessageBox.Show(confirmMessage, confirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.OK)
                {
                    return; // Cancelled by user - DO NOT SAVE!
                }

                var invoice = new Invoice
                {
                    InvoiceNo = TxtInvoiceNoDisplay.Text.Trim(),
                    PaperBillNo = paperBillNo,
                    CustomerId = 0,
                    FarmerId = _selectedFarmer.FarmerId,
                    CustomerName = _selectedFarmer.FarmerName,
                    MobileNumber = _selectedFarmer.MobileNumber,
                    VillageName = _selectedFarmer.VillageName,
                    InvoiceDate = DpBillDate.SelectedDate.HasValue ? DpBillDate.SelectedDate.Value.Date.Add(DateTime.Now.TimeOfDay) : DateTime.Now,
                    SubTotal = subTotal,
                    Discount = discountAmount,
                    TaxableAmount = taxable,
                    GstAmount = gstTotal,
                    RoundOff = 0,
                    GrandTotal = grandTotal,
                    PaymentMethod = payMode,
                    PaidAmount = paidAmount,
                    PayableAmount = payableAmount,
                    PaymentReference = TxtUpiRef.Text.Trim(),
                    Notes = "",
                    Status = "Active"
                };

                if (_editingInvoiceId > 0)
                {
                    // Update existing invoice
                    invoice.Id = _editingInvoiceId;
                    DatabaseHelper.UpdateInvoice(invoice, _items.ToList());
                    MessageBox.Show($"✓ Bill '{invoice.InvoiceNo}' updated successfully!\n\nGrand Total: ₹{grandTotal:N2}\nAmount Paid: ₹{paidAmount:N2}\nUdhar Balance: ₹{payableAmount:N2}",
                        "Bill Updated", MessageBoxButton.OK, MessageBoxImage.Information);

                    // After update, return to New Bill state
                    InitNewBill();
                }
                else
                {
                    int invoiceId = DatabaseHelper.SaveInvoice(invoice, _items.ToList());

                    MessageBox.Show($"✓ Bill '{invoice.InvoiceNo}' saved successfully!\n\nGrand Total: ₹{grandTotal:N2}\nAmount Paid: ₹{paidAmount:N2}\nUdhar Balance: ₹{payableAmount:N2}",
                        "Bill Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Open Invoice Details modal for PDF / Print / WhatsApp
                    try
                    {
                        var detailsWin = new InvoiceDetailsWindow(invoiceId);
                        var parent = Window.GetWindow(this);
                        if (parent != null) detailsWin.Owner = parent;
                        detailsWin.ShowDialog();
                    }
                    catch { }

                    InitNewBill();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save bill: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearBill_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count > 0 || _selectedFarmer != null)
            {
                var res = MessageBox.Show("Are you sure you want to clear the current bill?", "Clear Bill", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
            }
            InitNewBill();
        }
    }
}
