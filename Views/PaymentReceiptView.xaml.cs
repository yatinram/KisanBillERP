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
    public partial class PaymentReceiptView : UserControl
    {
        private int _page = 1;
        private int _pageSize = 10;
        private int _total = 0;
        private bool _isReady = false; // guards against events firing during InitializeComponent

        public PaymentReceiptView()
        {
            InitializeComponent();
            // mark ready after InitializeComponent so SelectionChanged fired during XAML parsing is ignored
            _isReady = true;
            LoadData();
        }

        private void LoadData()
        {
            if (TxtSearchPlaceholder == null || CmbPageSize == null) return;

            string query = TxtSearch.Text?.Trim() ?? string.Empty;
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Hidden;

            var res = DatabaseHelper.GetPaymentReceiptsPaged(query, _page, _pageSize);
            GridReceipts.ItemsSource = res.Items;
            _total = res.Total;

            int start = (_page - 1) * _pageSize + 1;
            int end = Math.Min(_page * _pageSize, _total);
            if (_total == 0)
            {
                start = 0;
                end = 0;
            }

            if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {start} to {end} of {_total} entries";

            int totalPages = (int)Math.Ceiling(_total / (double)_pageSize);
            BtnPrev.IsEnabled = _page > 1;
            BtnNext.IsEnabled = _page < totalPages;

            BuildPageNumberButtons(totalPages);
        }

        private void BuildPageNumberButtons(int totalPages)
        {
            if (PanelPageNumbers == null) return;

            PanelPageNumbers.Children.Clear();

            if (totalPages <= 1) return;

            int startPage = Math.Max(1, _page - 2);
            int endPage = Math.Min(totalPages, _page + 2);

            if (startPage > 1)
            {
                PanelPageNumbers.Children.Add(MakePageButton(1));
                if (startPage > 2) PanelPageNumbers.Children.Add(MakeEllipsis());
            }

            for (int i = startPage; i <= endPage; i++)
            {
                PanelPageNumbers.Children.Add(MakePageButton(i));
            }

            if (endPage < totalPages)
            {
                if (endPage < totalPages - 1) PanelPageNumbers.Children.Add(MakeEllipsis());
                PanelPageNumbers.Children.Add(MakePageButton(totalPages));
            }
        }

        private Button MakePageButton(int pageNumber)
        {
            var btn = new Button
            {
                Content = pageNumber.ToString(),
                Style = (Style)FindResource("PagerNumberButton"),
                Tag = pageNumber
            };

            if (pageNumber == _page)
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                btn.BorderBrush = btn.Background;
                btn.Foreground = Brushes.White;
                btn.FontWeight = FontWeights.Bold;
            }
            else
            {
                btn.Background = Brushes.White;
                btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB"));
                btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
                btn.FontWeight = FontWeights.Normal;
            }

            btn.Click += PageButton_Click;
            return btn;
        }

        private TextBlock MakeEllipsis()
        {
            return new TextBlock
            {
                Text = "…",
                Width = 24,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"))
            };
        }

        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int p)
            {
                _page = p;
                LoadData();
            }
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            _page = 1;
            LoadData();
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;
            if (CmbPageSize?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int size))
            {
                _pageSize = size;
                _page = 1;
                LoadData();
            }
        }

        private void CmbPageSize_SelectionChanged(object sender, SelectionChangedEventArgs e) => PageSize_Changed(sender, e);

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            _page = 1;
            LoadData();
        }

        private void BtnAddReceipt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new PaymentReceiptWindow();
                window.Owner = Window.GetWindow(this);
                window.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                KrushiBillERP.Data.Logger.Log(ex);
                MessageBox.Show($"Error opening payment receipt window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                try
                {
                    var preview = new PaymentReceiptPreviewWindow(id);
                    preview.Owner = Window.GetWindow(this);
                    preview.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error viewing payment receipt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                try
                {
                    PaymentReceiptPrintHelper.PrintReceipt(id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error printing receipt: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            int totalPages = (int)Math.Ceiling(_total / (double)_pageSize);
            if (_page < totalPages)
            {
                _page++;
                LoadData();
            }
        }
    }
}