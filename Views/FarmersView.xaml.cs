using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class FarmersView : UserControl
    {
        private int _page = 1;
        private int _pageSize = 20;
        private int _total = 0;
        private bool _isLoaded = false;
        private readonly DispatcherTimer _searchTimer;

        private const string SearchPlaceholder = "Search farmer name, mobile or village...";

        public FarmersView()
        {
            InitializeComponent();
            _isLoaded = true;
            SetupSearchPlaceholder();

            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _searchTimer.Tick += SearchTimer_Tick;

            this.Loaded += (s, e) =>
            {
                LoadStats();
                LoadFarmers();
            };
        }

        private void SetupSearchPlaceholder()
        {
            if (TxtSearch == null) return;
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = SearchPlaceholder;
                TxtSearch.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch == null) return;
            if (TxtSearch.Text == SearchPlaceholder)
            {
                TxtSearch.Text = string.Empty;
                TxtSearch.Foreground = System.Windows.SystemColors.ControlTextBrush;
            }
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TxtSearch == null) return;
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtSearch.Text = SearchPlaceholder;
                TxtSearch.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearch == null || TxtSearch.Text == SearchPlaceholder) return;
            _searchTimer?.Stop();
            _searchTimer?.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer?.Stop();
            _page = 1;
            LoadFarmers();
        }

        private void LoadStats()
        {
            if (TxtTotal == null || TxtActive == null) return;
            try
            {
                TxtTotal.Text = DatabaseHelper.GetTotalFarmers().ToString();
                TxtActive.Text = DatabaseHelper.GetActiveFarmersCount().ToString();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void LoadFarmers()
        {
            if (DgFarmers == null) return;
            try
            {
                // Show/Hide Edit+Delete column based on SuperAdmin permission
                if (ColFarmerEditDelete != null)
                    ColFarmerEditDelete.Visibility = AppSession.CanEditOrDelete ? Visibility.Visible : Visibility.Collapsed;

                var search = TxtSearch?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(search) && search == SearchPlaceholder) search = null;

                int status = -1;
                if (CmbStatusFilter?.SelectedItem is ComboBoxItem ci)
                {
                    if (!int.TryParse(ci.Tag?.ToString(), out status)) status = -1;
                }

                var res = DatabaseHelper.GetFarmersPaged(search, status, _page, _pageSize);
                DgFarmers.ItemsSource = res.Items ?? new List<Farmer>();
                _total = res.Total;

                var start = _total == 0 ? 0 : ((_page - 1) * _pageSize) + 1;
                var end = Math.Min(_page * _pageSize, _total);
                if (TxtShowingInfo != null) TxtShowingInfo.Text = $"Showing {start} to {end} of {_total} entries";
                if (TxtPageInfo != null) TxtPageInfo.Text = _page.ToString();

                var max = Math.Max(1, (int)Math.Ceiling((_total + 0.0m) / _pageSize));
                if (BtnPrev != null) BtnPrev.IsEnabled = _page > 1;
                if (BtnNext != null) BtnNext.IsEnabled = _page < max;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to load farmers: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Log(ex);
            }
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CmbPageSize?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int sz))
            {
                _pageSize = sz;
                _page = 1;
                LoadFarmers();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FarmerDialog();
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true)
            {
                LoadStats();
                _page = 1;
                LoadFarmers();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            SetupSearchPlaceholder();
            if (CmbStatusFilter != null) CmbStatusFilter.SelectedIndex = 0;
            _page = 1;
            LoadStats();
            LoadFarmers();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _page = 1;
                LoadFarmers();
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (DgFarmers == null || !this.IsLoaded) return;
            _page = 1;
            LoadFarmers();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1) { _page--; LoadFarmers(); }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var max = (int)Math.Ceiling((_total + 0.0m) / _pageSize);
            if (_page < max) { _page++; LoadFarmers(); }
        }

        private Farmer GetFarmerFromSender(object sender)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Farmer f) return f;
            if (DgFarmers?.SelectedItem is Farmer sf) return sf;
            return null;
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            var f = GetFarmerFromSender(sender);
            if (f != null)
            {
                var dlg = new FarmerDetailsDialog(f);
                dlg.Owner = Window.GetWindow(this);
                dlg.ShowDialog();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Edit farmer records.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var f = GetFarmerFromSender(sender);
            if (f != null)
            {
                var dlg = new FarmerDialog(DatabaseHelper.GetFarmerById(f.FarmerId) ?? f);
                dlg.Owner = Window.GetWindow(this);
                if (dlg.ShowDialog() == true)
                {
                    LoadStats();
                    LoadFarmers();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.CanEditOrDelete)
            {
                MessageBox.Show("Permission Denied: Only user 'yatin' can Delete farmer records.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var f = GetFarmerFromSender(sender);
            if (f != null)
            {
                var res = MessageBox.Show($"Are you sure you want to delete {f.FarmerName}?", "Delete Farmer?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    try
                    {
                        DatabaseHelper.DeleteOrDeactivateFarmer(f.FarmerId);
                        MessageBox.Show("Farmer deleted / deactivated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadStats();
                        LoadFarmers();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Unable to delete farmer: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
