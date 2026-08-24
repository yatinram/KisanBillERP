using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class PurchaseSelectWindow : Window
    {
        public Purchase SelectedPurchase { get; private set; }

        public PurchaseSelectWindow()
        {
            InitializeComponent();
            LoadPurchases();
        }

        private void LoadPurchases()
        {
            string search = TxtSearch?.Text?.Trim();
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch?.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

            var list = DatabaseHelper.GetPurchasesForReturnSelection(search);
            GridPurchases.ItemsSource = list;
        }

        private void TxtSearch_KeyUp(object sender, KeyEventArgs e)
        {
            LoadPurchases();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Purchase p)
            {
                SelectedPurchase = p;
                DialogResult = true;
                Close();
            }
        }

        private void GridPurchases_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridPurchases.SelectedItem is Purchase p)
            {
                SelectedPurchase = p;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
