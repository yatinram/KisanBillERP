using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KrushiBillERP.Models;
using KrushiBillERP.Data;

namespace KrushiBillERP.Views
{
    public partial class ProductsView : UserControl
    {
        private int _page = 1;
        private int _pageSize = 20;
        private int _total = 0;
        private bool _isLoaded = false;
        private readonly DispatcherTimer _searchTimer;

        public ProductsView()
        {
            InitializeComponent();
            _isLoaded = true;
            LoadFilters();
            LoadStats();
            LoadProducts();
            SetupSearchPlaceholder();
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _searchTimer.Tick += SearchTimer_Tick;
        }

        private const string SearchPlaceholder = "Search product, code, company or batch...";
        private void SetupSearchPlaceholder()
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = SearchPlaceholder;
                TxtSearch.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void TxtSearch_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (TxtSearch.Text == SearchPlaceholder)
            {
                TxtSearch.Text = string.Empty;
                TxtSearch.Foreground = System.Windows.SystemColors.ControlTextBrush;
            }
        }

        private void TxtSearch_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = SearchPlaceholder;
                TxtSearch.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // Ignore placeholder text
                if (TxtSearch.Text == SearchPlaceholder) return;
                // Debounce
                _searchTimer.Stop();
                _searchTimer.Start();
            }
            catch { }
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            _page = 1;
            LoadProducts();
        }

        private void LoadFilters()
        {
            var cats = DatabaseHelper.GetCategories();
            var listCats = new List<Category> { new Category { Id = 0, Name = "All Categories" } };
            listCats.AddRange(cats);
            PopupCmbCategory.ItemsSource = listCats;
            PopupCmbCategory.SelectedIndex = 0;

            // Company filter uses distinct company values from Products table
            var companies = DatabaseHelper.GetDistinctCompanies();
            var listComp = new List<string> { "All Companies" };
            listComp.AddRange(companies);
            PopupCmbCompany.ItemsSource = listComp;
            PopupCmbCompany.SelectedIndex = 0;
        }

        private void LoadStats()
        {
            try
            {
                TxtTotal.Text = DatabaseHelper.GetTotalProducts().ToString();
                TxtActive.Text = DatabaseHelper.GetActiveProductsCount().ToString();
                TxtLowStock.Text = DatabaseHelper.GetLowStockCount().ToString();
                TxtExpiring.Text = DatabaseHelper.GetExpiringProductsNextDays(15).Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to load statistics: " + ex.Message);
            }
        }

        private void LoadProducts()
        {
            try
            {
                // Show/Hide Edit+Delete column based on SuperAdmin permission
                if (ColProductEditDelete != null)
                    ColProductEditDelete.Visibility = AppSession.CanEditOrDelete ? Visibility.Visible : Visibility.Collapsed;

                var search = TxtSearch?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(search) && search == SearchPlaceholder) search = null;
                int cat = 0;
                if (PopupCmbCategory?.SelectedItem is Category cobj) cat = cobj.Id;
                else if (PopupCmbCategory?.SelectedItem is ComboBoxItem cbItem && int.TryParse(cbItem.Tag?.ToString(), out var cbcat)) cat = cbcat;
                int status = -1;
                if (PopupCmbStatus?.SelectedItem is ComboBoxItem ci)
                {
                    var tag = ci.Tag?.ToString();
                    if (!int.TryParse(tag, out status)) status = -1;
                }
                // Defensive: ensure UI controls created
                if (DgProducts == null || TxtPageInfo == null)
                {
                    // UI may not be fully loaded yet (designer/runtime timing). Defer loading briefly instead of showing an error.
                    Dispatcher.BeginInvoke(new Action(() => { LoadProducts(); }), DispatcherPriority.Loaded);
                    return;
                }

                // company filter
                string company = null;
                if (PopupCmbCompany?.SelectedItem is string s && !string.IsNullOrWhiteSpace(s) && s != "All Companies") company = s;
                var res = DatabaseHelper.GetProductsPaged(search, cat, company, status, _page, _pageSize);
                DgProducts.ItemsSource = res.Items;
                _total = res.Total;
                var start = _total == 0 ? 0 : ((_page - 1) * _pageSize) + 1;
                var end = _total == 0 ? 0 : Math.Min(_page * _pageSize, _total);
                if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {start} to {end} of {_total} entries";
                TxtPageInfo.Text = _page.ToString();
                // enable/disable pagination buttons
                var max = Math.Max(1, (int)Math.Ceiling((_total + 0.0m) / _pageSize));
                BtnPrev.IsEnabled = _page > 1;
                BtnNext.IsEnabled = _page < max;
            }
            catch (Exception ex)
            {
                // Show full exception for debugging; user can paste this to help debugging
                MessageBox.Show("Unable to load products: " + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CmbPageSize?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int sz))
            {
                _pageSize = sz;
                _page = 1;
                LoadProducts();
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Popup uses its own combo boxes; just open it
                PopupFilter.IsOpen = true;
            }
            catch { }
        }

        private void BtnApplyProductFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Popup combo boxes are the active filters; just close popup and reload
                PopupFilter.IsOpen = false;
                _page = 1;
                LoadProducts();
            }
            catch { PopupFilter.IsOpen = false; }
        }

        private void BtnResetProductFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PopupCmbCategory.SelectedIndex = 0;
                PopupCmbCompany.SelectedIndex = 0;
                PopupCmbStatus.SelectedIndex = 0;
                if (TxtSearch != null) TxtSearch.Text = string.Empty;

                PopupFilter.IsOpen = false;
                _page = 1;
                LoadProducts();
            }
            catch
            {
                PopupFilter.IsOpen = false;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProductDialog();
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                LoadStats();
                _page = 1;
                LoadProducts();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            SetupSearchPlaceholder();
            if (PopupCmbCategory != null) PopupCmbCategory.SelectedIndex = 0;
            if (PopupCmbCompany != null) PopupCmbCompany.SelectedIndex = 0;
            if (PopupCmbStatus != null) PopupCmbStatus.SelectedIndex = 0;
            _page = 1;
            LoadStats();
            LoadProducts();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _page = 1; LoadProducts();
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _page = 1; LoadProducts();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1) { _page--; LoadProducts(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var max = (int)Math.Ceiling((_total + 0.0m) / _pageSize);
            if (_page < max) { _page++; LoadProducts(); }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            var p = (sender as FrameworkElement)?.DataContext as Product;
            if (p == null) return;
            var full = DatabaseHelper.GetProductById(p.Id);
            var dlg = new ProductDetailsDialog(full);
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Edit products.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var p = (sender as FrameworkElement)?.DataContext as Product;
            if (p == null) return;
            var full = DatabaseHelper.GetProductById(p.Id);
            var dlg = new ProductDialog(full);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                LoadStats(); LoadProducts();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Delete products.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var p = (sender as FrameworkElement)?.DataContext as Product;
            if (p == null) return;
            var res = MessageBox.Show($"Are you sure you want to delete \"{p.Name}\"?", "Delete Product?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            try
            {
                DatabaseHelper.DeleteOrDeactivateProduct(p.Id);
                MessageBox.Show("Product deleted/marked inactive.");
                LoadStats(); LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to delete product: " + ex.Message);
            }
        }
    }
}

