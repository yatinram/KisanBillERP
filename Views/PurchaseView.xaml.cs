using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class PurchaseView : UserControl
    {
        private int _page = 1;
        private int _pageSize = 10;
        private int _total = 0;
        private bool _isLoaded = false;

        public PurchaseView()
        {
            InitializeComponent();
            _isLoaded = true;
            LoadData();
        }

        private void LoadData()
        {
            if (!_isLoaded) return;

            var search = TxtSearch?.Text?.Trim();
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch?.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

            var res = DatabaseHelper.GetPurchasesPaged(search, _page, _pageSize);
            if (GridPurchases != null) GridPurchases.ItemsSource = res.Items;
            _total = res.Total;

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_total / _pageSize));
            if (_page > totalPages) _page = totalPages;

            int start = _total == 0 ? 0 : (_page - 1) * _pageSize + 1;
            int end = Math.Min(_page * _pageSize, _total);
            if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {start} to {end} of {_total} entries";

            if (BtnPrev != null) BtnPrev.IsEnabled = _page > 1;
            if (BtnNext != null) BtnNext.IsEnabled = _page < totalPages;

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

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            _page = 1;
            LoadData();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new PurchaseEntryWindow();
                var parentWin = Window.GetWindow(this);
                if (parentWin != null) win.Owner = parentWin;
                win.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Purchase Entry window: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            _page = 1;
            LoadData();
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int id)
            {
                try
                {
                    string tempFolder = System.IO.Path.GetTempPath();
                    string tempPdf = System.IO.Path.Combine(tempFolder, $"Purchase_Voucher_{id}_{Guid.NewGuid():N}.pdf");

                    PurchasePdfHelper.GeneratePdf(id, tempPdf);

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPdf) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening Purchase PDF: {ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
    }
}
