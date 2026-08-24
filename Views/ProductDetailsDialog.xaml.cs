using System;
using System.Text;
using System.Windows;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class ProductDetailsDialog : Window
    {
        public ProductDetailsDialog(Product p)
        {
            InitializeComponent();
            if (p != null) LoadProduct(p);
        }

        private void LoadProduct(Product p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Product Name       : {p.Name}");
            sb.AppendLine($"Product Code       : {p.ProductCode}");
            sb.AppendLine($"Company Name       : {p.Company}");
            sb.AppendLine($"Category           : {p.CategoryName}");
            sb.AppendLine("");
            sb.AppendLine($"Pack Size          : {p.PackSize}");
            sb.AppendLine($"Unit               : {p.Unit}");
            sb.AppendLine("");
            sb.AppendLine($"Purchase Price     : {p.PurchasePrice:C}");
            sb.AppendLine($"Selling Price      : {p.SalePrice:C}");
            sb.AppendLine("");
            sb.AppendLine($"Batch Number       : {p.BatchNo}");
            sb.AppendLine($"Expiry Date        : {(p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("dd-MMM-yyyy") : "-")}");
            sb.AppendLine("");
            sb.AppendLine($"GST                : {p.GstPercent}%");
            sb.AppendLine($"Minimum Stock      : {p.ReorderLevel}");
            sb.AppendLine($"Current Stock      : {p.StockQty}");
            sb.AppendLine($"HSN Code           : {p.HSN}");
            sb.AppendLine("");
            sb.AppendLine($"Status             : {(p.Status==1?"Active":"Inactive")}");
            TxtBlock.Text = sb.ToString();
        }
    }
}
