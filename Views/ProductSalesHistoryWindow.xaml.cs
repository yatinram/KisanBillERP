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
            LoadSalesReturnHistory();
            LoadStockMovementHistory();
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

            // Gross sold
            int grossSold = sales.Sum(s => s.Qty);
            decimal grossRevenue = sales.Sum(s => s.Amount);
            int uniqueCustomers = sales.Select(s => s.CustomerName).Distinct().Count();

            // Deduct sales returns for NET stats
            var returns = DatabaseHelper.GetProductSalesReturnHistory(_productId);
            GridSalesReturns.ItemsSource = returns;  // set here so LoadSalesReturnHistory skips re-query

            int returnedQty = returns.Sum(r => r.ReturnQuantity);
            decimal returnedAmount = returns.Sum(r => r.Amount);

            int netSold = grossSold - returnedQty;
            decimal netRevenue = grossRevenue - returnedAmount;

            StatSold.Text = netSold.ToString("N0");
            StatRevenue.Text = $"₹ {netRevenue:N2}";
            StatCustomers.Text = uniqueCustomers.ToString("N0");
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

        private void LoadSalesReturnHistory()
        {
            // Data already loaded in LoadSalesHistory() to compute net stats — skip re-query if already set
            if (GridSalesReturns.ItemsSource == null)
            {
                var returns = DatabaseHelper.GetProductSalesReturnHistory(_productId);
                GridSalesReturns.ItemsSource = returns;
            }
        }

        private void LoadStockMovementHistory()
        {
            var movements = DatabaseHelper.GetProductStockMovementHistory(_productId);
            GridStockMovements.ItemsSource = movements;
        }

        private void SetActiveTab(string tab)
        {
            // Reset all 4 tabs styling
            var mutedColor = (Color)ColorConverter.ConvertFromString("#6C757D");
            TabSales.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabSales.Foreground = new SolidColorBrush(mutedColor);
            TabPurchases.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabPurchases.Foreground = new SolidColorBrush(mutedColor);
            TabSalesReturns.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabSalesReturns.Foreground = new SolidColorBrush(mutedColor);
            TabStockMovements.BorderBrush = new SolidColorBrush(Colors.Transparent);
            TabStockMovements.Foreground = new SolidColorBrush(mutedColor);

            GridSales.Visibility = Visibility.Collapsed;
            GridPurchases.Visibility = Visibility.Collapsed;
            GridSalesReturns.Visibility = Visibility.Collapsed;
            GridStockMovements.Visibility = Visibility.Collapsed;

            if (tab == "sales")
            {
                TabSales.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                TabSales.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                GridSales.Visibility = Visibility.Visible;

                var items = GridSales.ItemsSource as System.Collections.Generic.List<ProductSaleRecord>;
                TxtEmpty.Visibility = (items == null || items.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (items == null || items.Count == 0) TxtEmpty.Text = "No sales records found for this product yet.";
            }
            else if (tab == "purchases")
            {
                TabPurchases.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                TabPurchases.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                GridPurchases.Visibility = Visibility.Visible;

                var items = GridPurchases.ItemsSource as System.Collections.Generic.List<ProductPurchaseRecord>;
                TxtEmpty.Visibility = (items == null || items.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (items == null || items.Count == 0) TxtEmpty.Text = "No purchase records found for this product yet.";
            }
            else if (tab == "sales_returns")
            {
                TabSalesReturns.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                TabSalesReturns.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
                GridSalesReturns.Visibility = Visibility.Visible;

                var items = GridSalesReturns.ItemsSource as System.Collections.Generic.List<ProductSalesReturnRecord>;
                TxtEmpty.Visibility = (items == null || items.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (items == null || items.Count == 0) TxtEmpty.Text = "No customer return records found for this product yet.";
            }
            else if (tab == "stock_movements")
            {
                TabStockMovements.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B21A8"));
                TabStockMovements.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B21A8"));
                GridStockMovements.Visibility = Visibility.Visible;

                var items = GridStockMovements.ItemsSource as System.Collections.Generic.List<ProductStockMovementRecord>;
                TxtEmpty.Visibility = (items == null || items.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                if (items == null || items.Count == 0) TxtEmpty.Text = "No stock adjustment or supplier return records found for this product yet.";
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

        private void TabSalesReturns_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("sales_returns");
        }

        private void TabStockMovements_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("stock_movements");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
