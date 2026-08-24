using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KrushiBillERP.Models;
using KrushiBillERP.Data;

namespace KrushiBillERP.Views
{
    public class PurchaseItemDisplay
    {
        public PurchaseItem Item { get; set; }
        public string DisplayText { get; set; }
    }

    public partial class ProductDialog : Window
    {
        private Product _product;
        private List<PurchaseItemDisplay> _purchaseItemsDisplay;

        public ProductDialog(Product product = null)
        {
            InitializeComponent();
            _product = product ?? new Product();
            LoadCategories();
            LoadDataToFields();
        }

        private void LoadCategories()
        {
            var cats = DatabaseHelper.GetCategories();
            CmbCategory.ItemsSource = cats;
        }

        private void LoadDataToFields()
        {
            if (_product == null) return;
            bool isExisting = _product.Id > 0;

            if (isExisting)
            {
                TxtHeaderTitle.Text = "✏ Edit Product Details";
                TxtHeaderSubtitle.Text = "Edit selling price, category, minimum stock alert, or product details.";
                PanelPurchaseSelection.Visibility = Visibility.Collapsed;

                TxtName.Text = _product.Name;
                TxtName.IsReadOnly = false;
                TxtName.Background = System.Windows.Media.Brushes.White;

                TxtCode.Text = _product.ProductCode;
                TxtCode.IsReadOnly = false;
                TxtCode.Background = System.Windows.Media.Brushes.White;

                TxtCompany.Text = _product.Company;
                TxtCompany.IsReadOnly = false;
                TxtCompany.Background = System.Windows.Media.Brushes.White;

                TxtBatch.Text = _product.BatchNo;
                TxtBatch.IsReadOnly = false;
                TxtBatch.Background = System.Windows.Media.Brushes.White;

                if (_product.ExpiryDate.HasValue) DpExpiry.SelectedDate = _product.ExpiryDate.Value;
                DpExpiry.IsEnabled = true;

                TxtPurchasePrice.Text = _product.PurchasePrice.ToString("F2");
                TxtPurchasePrice.IsReadOnly = false;
                TxtPurchasePrice.Background = System.Windows.Media.Brushes.White;

                TxtSellingPrice.Text = _product.SalePrice.ToString("F2");
                TxtGst.Text = _product.GstPercent.ToString("F2");
                TxtGst.IsReadOnly = false;
                TxtGst.Background = System.Windows.Media.Brushes.White;

                TxtMinStock.Text = _product.ReorderLevel.ToString();
                TxtPackSize.Text = _product.PackSize.ToString();
                TxtHsn.Text = _product.HSN;

                TxtPurchaseStockQty.Text = _product.StockQty.ToString();
                TxtPurchaseStockQty.IsReadOnly = true;
            }
            else
            {
                TxtHeaderTitle.Text = "📦 Add Product (From Purchase Entry)";
                TxtHeaderSubtitle.Text = "Select a product from Purchase Entry. Purchased details are locked 🔒; set selling price and category below.";
                PanelPurchaseSelection.Visibility = Visibility.Visible;

                // Auto-generate unique product code / SKU
                TxtCode.Text = DatabaseHelper.GenerateNextProductCode();

                TxtPackSize.Text = "1";
                TxtMinStock.Text = "5";
                if (CmbUnit.Items.Count > 0) CmbUnit.SelectedIndex = 3; // KG / Piece default

                // Load Purchase Items from Database
                LoadPurchaseItems();
            }

            if (!string.IsNullOrWhiteSpace(_product.Unit))
            {
                foreach (var it in CmbUnit.Items)
                {
                    if ((it as ComboBoxItem)?.Content?.ToString() == _product.Unit)
                    {
                        CmbUnit.SelectedItem = it;
                        break;
                    }
                }
            }

            if (_product.CategoryId > 0)
            {
                var sel = (CmbCategory.ItemsSource as List<Category>)?.FirstOrDefault(x => x.Id == _product.CategoryId);
                if (sel != null) CmbCategory.SelectedItem = sel;
            }

            if (_product.Status == 0) CmbStatus.SelectedIndex = 1; else CmbStatus.SelectedIndex = 0;
        }

        private void LoadPurchaseItems()
        {
            try
            {
                var items = DatabaseHelper.GetPurchaseItemsForProductSelection();
                _purchaseItemsDisplay = items.Select(pi =>
                {
                    int totalQty = pi.Quantity + pi.FreeQuantity;
                    string qtyDesc = pi.FreeQuantity > 0 ? $"{totalQty} ({pi.Quantity} Paid + {pi.FreeQuantity} Free)" : $"{totalQty}";
                    return new PurchaseItemDisplay
                    {
                        Item = pi,
                        DisplayText = $"{pi.ProductName} (Company: {pi.Company}) - Batch: {(string.IsNullOrEmpty(pi.BatchNumber) ? "N/A" : pi.BatchNumber)} | Total Stock: {qtyDesc} | Purchase: ₹{pi.PurchasePrice:N2}"
                    };
                }).ToList();

                CmbPurchaseItem.ItemsSource = _purchaseItemsDisplay;
                CmbPurchaseItem.DisplayMemberPath = "DisplayText";

                if (_purchaseItemsDisplay.Count > 0)
                {
                    CmbPurchaseItem.SelectedIndex = 0;
                }
                else
                {
                    MessageBox.Show("No purchase entry records found in database. Please add a Purchase Entry first before adding products.", "Purchase Entry Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading purchase items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int _selectedPurchaseItemId = 0;

        private void CmbPurchaseItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbPurchaseItem.SelectedItem is PurchaseItemDisplay disp && disp.Item != null)
            {
                var item = disp.Item;
                _selectedPurchaseItemId = item.PurchaseItemId;
                TxtName.Text = item.ProductName;
                TxtCompany.Text = item.Company;
                TxtBatch.Text = item.BatchNumber;
                if (item.ExpiryDate.HasValue) DpExpiry.SelectedDate = item.ExpiryDate.Value;
                TxtPurchasePrice.Text = item.PurchasePrice.ToString("F2");
                TxtGst.Text = item.GST.ToString("F2");
                int totalStockQty = item.Quantity + item.FreeQuantity;
                TxtPurchaseStockQty.Text = totalStockQty.ToString();
                TxtHsn.Text = string.IsNullOrEmpty(item.HSN) ? "" : item.HSN;

                // Match and select Category if available from Purchase Entry
                if (!string.IsNullOrWhiteSpace(item.CategoryName))
                {
                    var catList = CmbCategory.ItemsSource as List<Category>;
                    var foundCat = catList?.FirstOrDefault(c => string.Equals(c.Name, item.CategoryName, StringComparison.OrdinalIgnoreCase));
                    if (foundCat != null)
                    {
                        CmbCategory.SelectedItem = foundCat;
                    }
                }

                // Lock details coming from Purchase Entry
                TxtName.IsReadOnly = true;
                TxtName.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                TxtCompany.IsReadOnly = true;
                TxtCompany.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                CmbCategory.IsEnabled = false;

                TxtBatch.IsReadOnly = true;
                TxtBatch.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                DpExpiry.IsEnabled = false;

                TxtPurchasePrice.IsReadOnly = true;
                TxtPurchasePrice.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                TxtGst.IsReadOnly = true;
                TxtGst.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                TxtPurchaseStockQty.IsReadOnly = true;
                TxtPurchaseStockQty.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                TxtHsn.IsReadOnly = true;
                TxtHsn.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F3F4F6"));

                // Focus on Selling Price for fast data entry
                TxtSellingPrice.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtName.Text)) throw new Exception("Please select a product from Purchase Entry.");
                if (string.IsNullOrWhiteSpace(TxtCode.Text)) throw new Exception("Product code is required.");
                if (CmbCategory.SelectedItem == null && CmbCategory.Items.Count > 0) CmbCategory.SelectedIndex = 0;
                if (CmbCategory.SelectedItem == null) throw new Exception("Please select a Category.");

                decimal.TryParse(TxtPurchasePrice.Text, out var pp);
                if (!decimal.TryParse(TxtSellingPrice.Text, out var sp) || sp <= 0) throw new Exception("Selling price must be a valid positive amount.");

                // Validate Selling Price must be greater than Purchase Price
                if (sp <= pp)
                {
                    throw new Exception($"Selling Price (₹{sp:N2}) must be strictly greater than Purchase Price (₹{pp:N2}).");
                }

                if (!decimal.TryParse(TxtPackSize.Text, out var pack) || pack <= 0) throw new Exception("Pack size must be a positive number.");
                if (!int.TryParse(TxtMinStock.Text, out var min) || min < 0) throw new Exception("Minimum stock alert must be >= 0.");

                decimal.TryParse(TxtGst.Text, out var gst);
                int.TryParse(TxtPurchaseStockQty.Text, out var psq);

                _product.Name = TxtName.Text.Trim();
                _product.ProductCode = TxtCode.Text.Trim();
                _product.Company = TxtCompany.Text.Trim();
                _product.CategoryId = (CmbCategory.SelectedItem as Category)?.Id ?? 0;
                _product.PackSize = pack;
                _product.BatchNo = TxtBatch.Text.Trim();
                _product.ExpiryDate = DpExpiry.SelectedDate;
                _product.Unit = (CmbUnit.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unit";
                _product.PurchasePrice = pp;
                _product.SalePrice = sp;
                _product.GstPercent = gst;
                _product.ReorderLevel = min;
                _product.PurchaseStockQty = psq;
                _product.HSN = TxtHsn.Text.Trim();
                _product.Status = (CmbStatus.SelectedItem as ComboBoxItem)?.Tag == null ? 1 : Convert.ToInt32((CmbStatus.SelectedItem as ComboBoxItem).Tag);

                bool isNew = _product.Id == 0;
                if (isNew && _product.PurchaseStockQty > 0)
                {
                    _product.StockQty = _product.PurchaseStockQty;
                }

                DatabaseHelper.SaveProduct(_product);

                if (isNew && _product.Id > 0)
                {
                    DatabaseHelper.SetProductStock(_product.Id, _product.StockQty);
                    if (_selectedPurchaseItemId > 0)
                    {
                        DatabaseHelper.UpdatePurchaseItemProductId(_selectedPurchaseItemId, _product.Id);
                    }
                }

                MessageBox.Show(isNew ? "✓ Product added to catalog successfully!" : "✓ Product updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
