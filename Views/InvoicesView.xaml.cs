using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class InvoicesView : Page
    {
        private int _page = 1;
        private int _pageSize = 10;
        private int _total = 0;
        private bool _isLoaded = false;
        private Farmer _selectedFarmer = null;

        private static readonly SolidColorBrush BrushGreen = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
        private static readonly SolidColorBrush BrushWhite = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush BrushSlate = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
        private static readonly SolidColorBrush BrushBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

        public InvoicesView()
        {
            InitializeComponent();
            _isLoaded = true;
            this.Loaded += (s, e) => LoadData();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Edit or modify bills.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int id))
            {
                try
                {
                    // Instead of opening the modal editor, navigate to the Billing view
                    // and load the selected invoice into the Add Bill section for in-page editing.
                    var owner = Window.GetWindow(this) as DashboardWindow;
                    if (owner == null)
                    {
                        MessageBox.Show("Unable to locate main window to open billing editor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    owner.OpenBillingViewWithInvoice(id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening invoice editor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Delete bills.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int invoiceId))
            {
                var confirm = MessageBox.Show($"Are you sure you want to delete Invoice #{invoiceId}? This will restore product stock and cannot be undone.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    bool success = DatabaseHelper.DeleteInvoice(invoiceId);
                    if (success)
                    {
                        MessageBox.Show("Invoice deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadData();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete invoice.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ===================== DATA LOADING & FILTERING =====================

        private void LoadData()
        {
            try
            {
                string search = TxtFarmerSearch.Text.Trim();
                HintFarmer.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

                DateTime? from = DpFrom.SelectedDate;
                DateTime? to = DpTo.SelectedDate;

                // Validate date range
                if (from.HasValue && to.HasValue && from.Value > to.Value)
                {
                    MessageBox.Show("Start Date cannot be greater than End Date.", "Date Filter Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int farmerId = _selectedFarmer != null ? _selectedFarmer.FarmerId : 0;

                var (items, total, _, _, _) = DatabaseHelper.GetInvoicesPaged(
                    search: string.IsNullOrWhiteSpace(search) ? null : search,
                    customerId: 0,
                    farmerId: farmerId,
                    paymentMethod: null,
                    dateRange: (from.HasValue || to.HasValue) ? "Custom" : "All Time",
                    customStart: from,
                    customEnd: to,
                    page: _page,
                    pageSize: _pageSize);

                _total = total;

                // Assign RowNo (#)
                int startRow = (_page - 1) * _pageSize + 1;
                foreach (var inv in items)
                {
                    inv.RowNo = startRow++;
                }

                // Empty state vs Grid
                if (items.Count == 0)
                {
                    PanelEmpty.Visibility = Visibility.Visible;
                    GridInvoices.Visibility = Visibility.Collapsed;
                    TxtEmptyTitle.Text = "No Bills Found";
                    TxtEmptySubtitle.Text = _selectedFarmer != null ? $"No bill records found for {_selectedFarmer.FarmerName}." : "No bill records found in system.";
                }
                else
                {
                    PanelEmpty.Visibility = Visibility.Collapsed;
                    GridInvoices.Visibility = Visibility.Visible;
                }

                GridInvoices.ItemsSource = items;

                // Ensure GridInvoices has focusable selection behavior like reports table
                GridInvoices.SelectedIndex = -1;

                // Badge & pager text
                TxtTotalBadge.Text = $"{_total} Bills";

                int fromRow = _total == 0 ? 0 : (_page - 1) * _pageSize + 1;
                int toRow = Math.Min(_page * _pageSize, _total);
                if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {fromRow} to {toRow} of {_total} entries";

                int totalPages = Math.Max(1, (int)Math.Ceiling((double)_total / _pageSize));
                BtnPrev.IsEnabled = _page > 1;
                BtnNext.IsEnabled = _page < totalPages;

                BuildPageButtons(totalPages);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading bill history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== DYNAMIC PAGINATION =====================

        private void BuildPageButtons(int totalPages)
        {
            PanelPageNums.Children.Clear();
            if (totalPages <= 1) return;

            int start = Math.Max(1, _page - 2);
            int end = Math.Min(totalPages, start + 4);

            for (int p = start; p <= end; p++)
            {
                int pg = p;
                bool isCurrent = (p == _page);

                var btn = new Button
                {
                    Content = p.ToString(),
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(3, 0, 3, 0),
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    Background = isCurrent ? BrushGreen : BrushWhite,
                    Foreground = isCurrent ? BrushWhite : BrushSlate,
                    BorderBrush = isCurrent ? BrushGreen : BrushBorder,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };

                var tpl = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
                border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                border.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                border.AppendChild(cp);
                tpl.VisualTree = border;
                btn.Template = tpl;

                btn.Click += (s, e) =>
                {
                    _page = pg;
                    LoadData();
                };
                PanelPageNums.Children.Add(btn);
            }
        }

        // ===================== FARMER SELECTION (AUTOCOMPLETE POPUP) =====================

        private void SetFarmerPopupVisible(bool visible)
        {
            if (PopupFarmer != null)
            {
                PopupFarmer.IsOpen = visible;
                PopupFarmer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool IsFarmerPopupVisible()
        {
            return PopupFarmer != null && (PopupFarmer.IsOpen || PopupFarmer.Visibility == Visibility.Visible);
        }

        private void BtnSelectFarmer_Click(object sender, RoutedEventArgs e)
        {
            // Legacy - no longer used, farmer selection is inline
            TxtFarmerSearch.Focus();
        }

        private void TxtFarmerSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Escape) return;

            if (e.Key == Key.Enter)
            {
                SetFarmerPopupVisible(false);
                _page = 1;
                LoadData();
                return;
            }

            string search = TxtFarmerSearch.Text.Trim();
            HintFarmer.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            if (search.Length >= 1)
            {
                var matches = DatabaseHelper.SearchFarmersForPayment(search);
                ListFarmers.ItemsSource = matches;
                SetFarmerPopupVisible(matches.Count > 0);
            }
            else
            {
                SetFarmerPopupVisible(false);
                if (_selectedFarmer != null)
                {
                    _selectedFarmer = null;
                    TxtMobile.Text = string.Empty;
                    TxtVillage.Text = string.Empty;
                }
                _page = 1;
                LoadData();
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
                if (item != null) item.Focus();
                else ListFarmers.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && IsFarmerPopupVisible() && ListFarmers.Items.Count > 0)
            {
                SelectFarmer(ListFarmers.SelectedItem as Farmer ?? ListFarmers.Items[0] as Farmer);
                e.Handled = true;
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
            else if (e.Key == Key.Up && ListFarmers.SelectedIndex <= 0)
            {
                TxtFarmerSearch.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ListFarmers.SelectedItem is Farmer f)
            {
                SelectFarmer(f);
                e.Handled = true;
            }
        }

        private void ListFarmers_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ListFarmers.SelectedItem is Farmer f)
            {
                SelectFarmer(f);
            }
        }

        private void ListFarmers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Kept for backward compatibility - no-op
        }

        private void SelectFarmer(Farmer f)
        {
            if (f == null) return;
            _selectedFarmer = f;
            TxtFarmerSearch.Text = f.FarmerName;
            HintFarmer.Visibility = Visibility.Collapsed;
            SetFarmerPopupVisible(false);

            TxtMobile.Text = string.IsNullOrEmpty(f.MobileNumber) ? "-" : f.MobileNumber;
            TxtVillage.Text = string.IsNullOrEmpty(f.VillageName) ? "-" : f.VillageName;

            _page = 1;
            LoadData();
        }

        private void BtnClearFarmer_Click(object sender, RoutedEventArgs e)
        {
            _selectedFarmer = null;
            TxtFarmerSearch.Text = string.Empty;
            HintFarmer.Visibility = Visibility.Visible;
            PopupFarmer.Visibility = Visibility.Collapsed;

            TxtMobile.Text = string.Empty;
            TxtVillage.Text = string.Empty;
            DpFrom.SelectedDate = null;
            DpTo.SelectedDate = null;

            _page = 1;
            LoadData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _selectedFarmer = null;
            TxtFarmerSearch.Text = string.Empty;
            HintFarmer.Visibility = Visibility.Visible;
            PopupFarmer.Visibility = Visibility.Collapsed;
            TxtMobile.Text = string.Empty;
            TxtVillage.Text = string.Empty;
            DpFrom.SelectedDate = null;
            DpTo.SelectedDate = null;
            PopupDpFrom.SelectedDate = null;
            PopupDpTo.SelectedDate = null;
            _page = 1;
            LoadData();
        }

        // ===================== AUTOMATIC DATE FILTERING =====================

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            _page = 1;
            LoadData();
        }

        // ===================== FILTER POPUP HANDLERS =====================

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pre-populate popup date pickers from current selections
                PopupDpFrom.SelectedDate = DpFrom.SelectedDate;
                PopupDpTo.SelectedDate = DpTo.SelectedDate;
                PopupFilter.IsOpen = true;
            }
            catch { }
        }

        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Apply popup selections back to main date pickers and reload
                DpFrom.SelectedDate = PopupDpFrom.SelectedDate;
                DpTo.SelectedDate = PopupDpTo.SelectedDate;
                PopupFilter.IsOpen = false;
                _page = 1;
                LoadData();
            }
            catch { PopupFilter.IsOpen = false; }
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PopupDpFrom.SelectedDate = null;
                PopupDpTo.SelectedDate = null;
            }
            catch { }
        }

        // ===================== PAGE SIZE & NAV =====================



        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1)
            {
                _page--;
                LoadData();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)_total / _pageSize);
            if (_page < totalPages)
            {
                _page++;
                LoadData();
            }
        }

        private void CmbPageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CmbPageSize?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int sz))
            {
                _pageSize = sz;
                _page = 1;
                LoadData();
            }
        }

        // ===================== PDF ACTION ONLY =====================

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int invoiceId)
            {
                try
                {
                    string tempFolder = System.IO.Path.GetTempPath();
                    string tempPdf = System.IO.Path.Combine(tempFolder, $"Invoice_Preview_{invoiceId}_{Guid.NewGuid():N}.pdf");

                    InvoicePdfHelper.GeneratePdf(invoiceId, tempPdf);

                    Process.Start(new ProcessStartInfo(tempPdf) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error viewing PDF: {ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEditRow_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el)
                el.Visibility = AppSession.CanEditOrDelete ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnDeleteRow_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el)
                el.Visibility = AppSession.CanEditOrDelete ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
