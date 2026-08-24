using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class StockAdjustmentWindow : Window
    {
        private Product _selectedProduct;
        private readonly int _initialProductId;

        public StockAdjustmentWindow(int productId = 0)
        {
            InitializeComponent();
            _initialProductId = productId;
            LoadProducts();
        }

        private void LoadProducts()
        {
            try
            {
                var products = DatabaseHelper.GetProducts();
                CmbProduct.ItemsSource = products;

                if (_initialProductId > 0)
                {
                    var match = products.FirstOrDefault(p => p.Id == _initialProductId);
                    if (match != null)
                    {
                        CmbProduct.SelectedItem = match;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Product product)
            {
                _selectedProduct = product;
                TxtBatch.Text = product.BatchNo ?? "-";
                TxtCurrentStock.Text = product.StockQty.ToString();
            }
            else
            {
                _selectedProduct = null;
                TxtBatch.Text = string.Empty;
                TxtCurrentStock.Text = string.Empty;
            }
        }

        private void CmbReason_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelSpecifyReason == null) return;

            if (CmbReason.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content?.ToString() == "Other")
            {
                PanelSpecifyReason.Visibility = Visibility.Visible;
            }
            else
            {
                PanelSpecifyReason.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null)
            {
                MessageBox.Show("Please select a product.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtQuantity.Text?.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid quantity greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtQuantity.Focus();
                return;
            }

            if (CmbType.SelectedItem == null)
            {
                MessageBox.Show("Please select an adjustment type.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbReason.SelectedItem == null)
            {
                MessageBox.Show("Please select a reason.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string typeText = ((ComboBoxItem)CmbType.SelectedItem).Content.ToString();
            string type = "SET";
            if (typeText.Contains("+")) type = "ADD";
            else if (typeText.Contains("-")) type = "REDUCE";

            string reason = ((ComboBoxItem)CmbReason.SelectedItem).Content.ToString();
            if (reason == "Other")
            {
                if (string.IsNullOrWhiteSpace(TxtSpecifyReason.Text))
                {
                    MessageBox.Show("Please specify a reason.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtSpecifyReason.Focus();
                    return;
                }
                reason = TxtSpecifyReason.Text.Trim();
            }

            try
            {
                var adjustment = new StockAdjustment
                {
                    ProductId = _selectedProduct.Id,
                    AdjustmentType = type,
                    DeltaQty = qty,
                    NewQty = qty, // Used if type == 'SET'
                    Reason = reason,
                    Notes = TxtNotes.Text?.Trim(),
                    AdjustedBy = "Admin",
                    CreatedAt = DateTime.Now
                };

                DatabaseHelper.SaveStockAdjustment(adjustment);
                MessageBox.Show("Stock adjustment saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving adjustment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
