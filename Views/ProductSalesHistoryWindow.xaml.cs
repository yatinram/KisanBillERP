using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class ProductSalesHistoryWindow : Window
    {
        private readonly int _productId;
        private readonly Product _product;

        public ProductSalesHistoryWindow(int productId)
        {
            InitializeComponent();
            _productId = productId;

            // Load product info
            var allProducts = DatabaseHelper.GetProducts();
            _product = allProducts.FirstOrDefault(p => p.Id == productId);

            if (_product == null)
            {
                MessageBox.Show("Product not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadProductInfo();
            LoadSalesHistory();
            LoadPurchaseHistory();
            SetActiveTab("sales");
        }

        private void LoadProductInfo()
        {
            TxtTitle.Text = $"📊 Product History — {_product.Name}";
            TxtProductName.Text = _product.Name;
            TxtCompany.Text = _product.Company ?? "-";
            TxtCurrentStock.Text = $"{_product.StockQty} {_product.Unit}";
            TxtBatchExpiry.Text = $"{_product.BatchNo} / {_product.ExpiryDisplay}";
            TxtPrice.Text = $"₹ {_product.SalePrice:N2}";
        }

        private void LoadSalesHistory()
        {
            var sales = DatabaseHelper.GetProductSalesHistory(_productId);
            GridSales.ItemsSource = sales;

            // Sales summary stats
            int totalSold = sales.Sum(s => s.Qty);
            decimal totalRevenue = sales.Sum(s => s.Amount);
            int uniqueCustomers = sales.Select(s => s.CustomerName).Distinct().Count();

            StatSold.Text = totalSold.ToString("N0");
            StatRevenue.Text = $"₹ {totalRevenue:N2}";
            StatCustomers.Text = uniqueCustomers.ToString("N0");

            if (sales.Count == 0 && GridPurchases.Visibility == Visibility.Collapsed)
            {
                TxtEmpty.Visibility = Visibility.Visible;
                TxtEmpty.Text = "No sales records found for this product yet.";
            }
            else
            {
                TxtEmpty.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadPurchaseHistory()
        {
            var purchases = DatabaseHelper.GetProductPurchaseHistory(_productId);
            GridPurchases.ItemsSource = purchases;

            // Purchase summary (Net Purchased = Entries - Returns)
            int entryQty = purchases.Where(p => p.TransactionType == "Purchase Entry").Sum(p => p.TotalQty);
            int returnQty = purchases.Where(p => p.TransactionType == "Purchase Return").Sum(p => p.TotalQty);
            int netPurchased = entryQty - returnQty;
            StatPurchased.Text = netPurchased.ToString("N0");
        }

        private void SetActiveTab(string tab)
        {
            // Reset both tabs
            TabSales.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabSales.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
            TabPurchases.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabPurchases.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));

            if (tab == "sales")
            {
                TabSales.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                TabSales.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                GridSales.Visibility = Visibility.Visible;
                GridPurchases.Visibility = Visibility.Collapsed;

                var sales = GridSales.ItemsSource as System.Collections.Generic.List<ProductSaleRecord>;
                TxtEmpty.Visibility = (sales == null || sales.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (sales == null || sales.Count == 0)
                    TxtEmpty.Text = "No sales records found for this product yet.";
            }
            else
            {
                TabPurchases.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                TabPurchases.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                GridSales.Visibility = Visibility.Collapsed;
                GridPurchases.Visibility = Visibility.Visible;

                var purchases = GridPurchases.ItemsSource as System.Collections.Generic.List<ProductPurchaseRecord>;
                TxtEmpty.Visibility = (purchases == null || purchases.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (purchases == null || purchases.Count == 0)
                    TxtEmpty.Text = "No purchase records found for this product yet.";
            }
        }

        private void TabSales_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("sales");
        }

        private void TabPurchases_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("purchases");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
