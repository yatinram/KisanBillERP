using System;
using System.Windows;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class ProductDetailsWindow : Window
    {
        public ProductDetailsWindow(Product p)
        {
            InitializeComponent();
            if (p == null) return;
            LblName.Text = p.Name;
            LblCode.Text = p.ProductCode;
            LblCompany.Text = p.Company;
            LblCategory.Text = p.CategoryName;
            LblPack.Text = p.PackSize.ToString();
            LblUnit.Text = p.Unit;
            LblPurchasePrice.Text = p.PurchasePrice.ToString("C");
            LblSalePrice.Text = p.SalePrice.ToString("C");
            LblBatch.Text = p.BatchNo;
            LblExpiry.Text = p.ExpiryDate?.ToString("dd-MMM-yyyy") ?? "";
            LblGst.Text = p.GstPercent + "%";
            LblMinStock.Text = p.ReorderLevel.ToString();
            LblStock.Text = p.StockQty.ToString();
            LblHsn.Text = p.HSN;
            LblStatus.Text = p.Status == 1 ? "Active" : "Inactive";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
