using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;
namespace KrushiBillERP.Views
{
    public partial class StockView : UserControl
    {
        private int _page = 1;
        private int _pageSize = 10;
        private int _total = 0;
        private bool _isLoaded = false;

        public StockView()
        {
            InitializeComponent();
            LoadCategoryFilter();
            _isLoaded = true;
            LoadData();
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Populate popup controls from current filters
                PopupCmbCategory.ItemsSource = CmbCategoryFilter.ItemsSource;
                PopupCmbCategory.SelectedIndex = CmbCategoryFilter.SelectedIndex;

                // Match status selection
                var status = (CmbStatusFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Stock Levels";
                foreach (ComboBoxItem it in PopupCmbStatus.Items)
                {
                    if (it.Content?.ToString() == status)
                    {
                        PopupCmbStatus.SelectedItem = it;
                        break;
                    }
                }

                PopupFilter.IsOpen = true;
            }
            catch { }
        }

        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PopupCmbCategory.SelectedItem != null)
                {
                    CmbCategoryFilter.SelectedItem = PopupCmbCategory.SelectedItem;
                }

                if (PopupCmbStatus.SelectedItem is ComboBoxItem item)
                {
                    CmbStatusFilter.SelectedIndex = PopupCmbStatus.Items.IndexOf(item);
                }

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
                PopupCmbCategory.SelectedIndex = 0;
                PopupCmbStatus.SelectedIndex = 0;
            }
            catch { }
        }



        private void LoadCategoryFilter()
        {
            var categories = DatabaseHelper.GetCategories();
            categories.Insert(0, new Category { Id = 0, Name = "All Categories" });
            CmbCategoryFilter.ItemsSource = categories;
            CmbCategoryFilter.SelectedIndex = 0;
        }

        private void LoadData()
        {
            if (!_isLoaded) return;

            string search = TxtSearch?.Text?.Trim();
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch?.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

            var cat = CmbCategoryFilter?.SelectedItem as Category;
            int catId = cat?.Id ?? 0;

            string statusFilter = (CmbStatusFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Stock Levels";

            var res = DatabaseHelper.GetProductsPaged(search, catId, null, -1, _page, _pageSize);
            var items = res.Items;

            // Apply stock status filter in-memory if specific status selected
            if (statusFilter == "Low Stock")
            {
                items = items.Where(p => p.StockQty > 0 && p.StockQty <= p.ReorderLevel).ToList();
            }
            else if (statusFilter == "Out of Stock")
            {
                items = items.Where(p => p.StockQty <= 0).ToList();
            }
            else if (statusFilter == "Expiring Soon")
            {
                items = items.Where(p => p.IsExpiringSoon).ToList();
            }

            GridStock.ItemsSource = items;
            _total = res.Total;

            // Stats
            int totalSkus = DatabaseHelper.GetTotalProducts();
            int lowStockCount = DatabaseHelper.GetLowStockCount();
            var expiringList = DatabaseHelper.GetExpiringProductsNextDays(15);
            var allProds = DatabaseHelper.GetProducts();
            decimal valuation = allProds.Sum(p => p.StockQty * p.PurchasePrice);

            StatTotalSkus.Text = totalSkus.ToString("N0");
            StatValuation.Text = $"₹ {valuation:N2}";
            StatLowStock.Text = lowStockCount.ToString("N0");
            StatExpiring.Text = expiringList.Count.ToString("N0");

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_total / _pageSize));
            if (_page > totalPages) _page = totalPages;

            int start = _total == 0 ? 0 : (_page - 1) * _pageSize + 1;
            int end = Math.Min(_page * _pageSize, _total);
            if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {start} to {end} of {_total} entries";

            BtnPrev.IsEnabled = _page > 1;
            BtnNext.IsEnabled = _page < totalPages;

            BuildPageNumberButtons(totalPages);
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CmbPageSize?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int sz))
            {
                _pageSize = sz;
                _page = 1;
                LoadData();
            }
        }

        private void BuildPageNumberButtons(int totalPages)
        {
            if (PanelPageNumbers == null) return;
            PanelPageNumbers.Children.Clear();
            int startPage = Math.Max(1, _page - 2);
            int endPage = Math.Min(totalPages, startPage + 4);
            if (endPage - startPage < 4)
            {
                startPage = Math.Max(1, endPage - 4);
            }

            for (int p = startPage; p <= endPage; p++)
            {
                int pageNum = p;
                var btn = new Button
                {
                    Content = pageNum.ToString(),
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(2, 0, 2, 0),
                    FontSize = 13,
                    FontWeight = pageNum == _page ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = pageNum == _page ? Brushes.White : (Brush)FindResource("HeadingText"),
                    Background = pageNum == _page ? (Brush)FindResource("AccentGreen") : Brushes.White,
                    BorderBrush = (Brush)FindResource("CardBorder"),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };

                btn.Click += (s, e) =>
                {
                    _page = pageNum;
                    LoadData();
                };

                PanelPageNumbers.Children.Add(btn);
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            _page = 1;
            LoadData();
        }

        private void Filter_Changed(object sender, KeyEventArgs e)
        {
            _page = 1;
            LoadData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            CmbCategoryFilter.SelectedIndex = 0;
            CmbStatusFilter.SelectedIndex = 0;
            _page = 1;
            LoadData();
        }

        private void BtnNewAdjustment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new StockAdjustmentWindow();
                var parentWin = Window.GetWindow(this);
                if (parentWin != null) win.Owner = parentWin;
                if (win.ShowDialog() == true)
                {
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening stock adjustment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAdjustRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int productId)
            {
                try
                {
                    var win = new StockAdjustmentWindow(productId);
                    var parentWin = Window.GetWindow(this);
                    if (parentWin != null) win.Owner = parentWin;
                    if (win.ShowDialog() == true)
                    {
                        LoadData();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening stock adjustment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnAdjustmentLog_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Stock adjustment audit log has been updated in database. You can also view details in Reports > Stock Report.", "Stock Audit Log", MessageBoxButton.OK, MessageBoxImage.Information);
        }

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
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_total / _pageSize));
            if (_page < totalPages)
            {
                _page++;
                LoadData();
            }
        }
        private void BtnHistoryRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int productId)
            {
                try
                {
                    var win = new ProductSalesHistoryWindow(productId);
                    var parentWin = Window.GetWindow(this);
                    if (parentWin != null) win.Owner = parentWin;
                    win.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening product history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
