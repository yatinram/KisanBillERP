using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KrushiBillERP.Models;
using KrushiBillERP.Data;

namespace KrushiBillERP.Views
{
    public partial class FarmerSelectWindow : Window
    {
        public Farmer SelectedFarmer { get; private set; }

        public FarmerSelectWindow()
        {
            InitializeComponent();
            LoadFarmers();
        }

        private void LoadFarmers()
        {
            if (TxtSearch == null || TxtSearchPlaceholder == null || GridFarmers == null) return;

            string search = TxtSearch.Text?.Trim() ?? string.Empty;
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(search) ? Visibility.Visible : Visibility.Collapsed;

            try
            {
                var farmers = DatabaseHelper.SearchFarmersForPayment(search);
                GridFarmers.ItemsSource = farmers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading farmers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            LoadFarmers();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Farmer farmer)
            {
                SelectFarmer(farmer);
            }
        }

        private void GridFarmers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridFarmers.SelectedItem is Farmer farmer)
            {
                SelectFarmer(farmer);
            }
        }

        private void SelectFarmer(Farmer farmer)
        {
            SelectedFarmer = farmer;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
