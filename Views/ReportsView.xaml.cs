using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class ReportsView : UserControl
    {
        private bool _isLoaded = false;
        private List<object> _allItems = new List<object>();
        private int _currentPage = 1;
        private int _pageSize = 10;

        public ReportsView()
        {
            InitializeComponent();
            _isLoaded = true;
            // ensure dropdown default selected triggers report load
            LoadCurrentTabReport();
        }


        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            LoadCurrentTabReport();
        }

        private void DateRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            LoadCurrentTabReport();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _currentPage = 1;
            LoadCurrentTabReport();
        }

        private void CmbReportSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            LoadCurrentTabReport();
        }

        private void CmbActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var cb = sender as ComboBox;
            var item = cb?.SelectedItem as ComboBoxItem;
            if (item == null) return;
            var tag = (item.Tag as string) ?? "";
            try
            {
                switch (tag)
                {
                    case "Export":
                        BtnExportCsv_Click(this, new RoutedEventArgs());
                        break;
                    case "SavePdf":
                        BtnSavePdf_Click(this, new RoutedEventArgs());
                        break;
                    case "Print":
                        BtnPrintReport_Click(this, new RoutedEventArgs());
                        break;
                }
            }
            finally
            {
                // reset to placeholder
                cb.SelectedIndex = 0;
            }
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _pageSize = int.Parse((CmbPageSize.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "10");
            _currentPage = 1;
            RenderPage();
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RenderPage();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_allItems.Count / (double)_pageSize));
            if (_currentPage < totalPages)
            {
                _currentPage++;
                RenderPage();
            }
        }

        private void RenderPage()
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_allItems.Count / (double)_pageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pageItems = _allItems.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();
            GridReport.ItemsSource = pageItems;
            TxtPageInfo.Text = _currentPage.ToString();
            BtnPrevPage.IsEnabled = _currentPage > 1;
            BtnNextPage.IsEnabled = _currentPage < totalPages;

            // Update showing range info for pagination
            int total = _allItems?.Count ?? 0;
            int start = total == 0 ? 0 : ((_currentPage - 1) * _pageSize) + 1;
            int end = total == 0 ? 0 : Math.Min(_currentPage * _pageSize, total);
            TxtShowingInfo.Text = $"Showing {start} to {end} of {total} entries";
        }

        private void GridReport_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            try
            {
                // center cells and headers for No and InvoiceNo columns
                if (e.PropertyName == "No" || e.PropertyName == "InvoiceNo")
                {
                    if (e.Column is DataGridTextColumn txtCol)
                    {
                        var elementStyle = new Style(typeof(TextBlock));
                        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                        txtCol.ElementStyle = elementStyle;

                        var headerStyle = new Style(typeof(DataGridColumnHeader));
                        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                        txtCol.HeaderStyle = headerStyle;
                    }
                }
            }
            catch
            {
                // swallow any styling errors to avoid breaking auto-generation
            }
        }

        private void SetStat(int index, string icon, string title, string value, string sub)
        {
            switch (index)
            {
                case 1:
                    TxtStat1Icon.Text = icon; TxtStat1Title.Text = title; TxtStat1Value.Text = value; TxtStat1Sub.Text = sub;
                    break;
                case 2:
                    TxtStat2Icon.Text = icon; TxtStat2Title.Text = title; TxtStat2Value.Text = value; TxtStat2Sub.Text = sub;
                    break;
                case 3:
                    TxtStat3Icon.Text = icon; TxtStat3Title.Text = title; TxtStat3Value.Text = value; TxtStat3Sub.Text = sub;
                    break;
                case 4:
                    TxtStat4Icon.Text = icon; TxtStat4Title.Text = title; TxtStat4Value.Text = value; TxtStat4Sub.Text = sub;
                    break;
            }
        }

        private void LoadCurrentTabReport()
        {
            string dateRange = (CmbDateRange?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Today";

            // Prefer dropdown selector if present
            var selTag = (CmbReportSelector?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(selTag))
            {
                switch (selTag.ToLowerInvariant())
                {
                    case "sales":
                        LoadSalesReport(dateRange);
                        break;
                    case "stock":
                        LoadStockReport();
                        break;
                    case "udhar":
                        LoadUdharReport();
                        break;
                    case "gst":
                        LoadGstReport(dateRange);
                        break;
                    case "purchase":
                        LoadPurchaseReport(dateRange);
                        break;
                }
                return;
            }

            // If no dropdown present, default to Sales report
            LoadSalesReport(dateRange);
        }

        private void LoadSalesReport(string dateRange)
        {
            var res = DatabaseHelper.GetInvoicesPaged(search: null, customerId: 0, farmerId: 0, paymentMethod: "All", dateRange: dateRange, customStart: null, customEnd: null, page: 1, pageSize: 1000);
            var items = res.Items.Select((inv, idx) => new
            {
                No = idx + 1,
                InvoiceNo = inv.InvoiceNo,
                FarmerName = string.IsNullOrWhiteSpace(inv.CustomerName) ? "Walk-in Farmer" : inv.CustomerName,
                Date = inv.InvoiceDate.ToString("dd MMM yyyy, hh:mm tt"),
                Taxable = $"₹ {inv.SubTotal:N2}",
                GST = $"₹ {inv.GstAmount:N2}",
                GrandTotal = $"₹ {inv.GrandTotal:N2}",
                PaymentMode = inv.PaymentMethod
            }).ToList();

            _allItems = items.Cast<object>().ToList();
            _currentPage = 1;
            RenderPage();

            decimal avgBill = res.Total > 0 ? res.TotalGrand / res.Total : 0;
            SetStat(1, "📄", $"Total Bills ({dateRange})", res.Total.ToString(), "Total invoices generated");
            SetStat(2, "₹", $"Total Revenue ({dateRange})", $"₹ {res.TotalGrand:N2}", "Total amount collected");
            SetStat(3, "📊", $"Total GST ({dateRange})", $"₹ {res.TotalGst:N2}", "Total GST amount");
            SetStat(4, "📈", "Average Bill Value", $"₹ {avgBill:N2}", "Average per invoice");

            TxtReportSummary.Text = $"Sales & Billing Report ({dateRange}): {res.Total} Bills | Total Revenue: ₹ {res.TotalGrand:N2} | Total GST: ₹ {res.TotalGst:N2}";
        }

        private void LoadStockReport()
        {
            var prods = DatabaseHelper.GetProducts();
            decimal valuation = prods.Sum(p => p.StockQty * p.PurchasePrice);
            int lowStock = prods.Count(p => p.StockQty <= p.ReorderLevel);
            int totalUnits = prods.Sum(p => p.StockQty);

            var items = prods.Select((p, idx) => new
            {
                No = idx + 1,
                ProductCode = p.ProductCode,
                Name = p.Name,
                Category = p.CategoryName,
                Company = p.Company,
                Batch = p.BatchNo,
                Expiry = p.ExpiryDisplay,
                StockQty = p.StockQty,
                Unit = p.Unit,
                PurchasePrice = $"₹ {p.PurchasePrice:N2}",
                SellingPrice = $"₹ {p.SalePrice:N2}",
                Valuation = $"₹ {(p.StockQty * p.PurchasePrice):N2}",
                Status = p.StockStatus
            }).ToList();

            _allItems = items.Cast<object>().ToList();
            _currentPage = 1;
            RenderPage();

            SetStat(1, "📦", "Total SKUs", prods.Count.ToString(), "Total products");
            SetStat(2, "₹", "Total Valuation", $"₹ {valuation:N2}", "Stock value at purchase price");
            SetStat(3, "⚠️", "Low Stock Alerts", lowStock.ToString(), "Items below reorder level");
            SetStat(4, "📈", "Total Stock Units", totalUnits.ToString(), "Across all products");

            TxtReportSummary.Text = $"Stock Valuation Report: {prods.Count} SKUs | Total Valuation: ₹ {valuation:N2} | Low Stock Alerts: {lowStock}";
        }

        private void LoadUdharReport()
        {
            var farmers = DatabaseHelper.SearchFarmersForPayment("");
            var list = new List<object>();
            decimal totalOutstanding = 0m;
            decimal maxBalance = 0m;
            int idx = 1;

            foreach (var f in farmers)
            {
                decimal bal = DatabaseHelper.GetFarmerOutstandingBalance(f.FarmerId);
                if (bal > 0)
                {
                    list.Add(new
                    {
                        No = idx++,
                        FarmerName = f.FarmerName,
                        Mobile = f.MobileNumber,
                        Village = f.VillageName,
                        OutstandingBalance = $"₹ {bal:N2}"
                    });
                    totalOutstanding += bal;
                    if (bal > maxBalance) maxBalance = bal;
                }
            }

            _allItems = list;
            _currentPage = 1;
            RenderPage();

            decimal avgBalance = list.Count > 0 ? totalOutstanding / list.Count : 0;
            SetStat(1, "👥", "Farmers with Balance", list.Count.ToString(), "Total farmers");
            SetStat(2, "₹", "Total Outstanding", $"₹ {totalOutstanding:N2}", "Total udhar amount");
            SetStat(3, "📊", "Average Balance", $"₹ {avgBalance:N2}", "Per farmer average");
            SetStat(4, "🏆", "Highest Balance", $"₹ {maxBalance:N2}", "Largest single outstanding");

            TxtReportSummary.Text = $"Udhar & Customer Balance Report: {list.Count} Farmers with Balance | Total Outstanding: ₹ {totalOutstanding:N2}";
        }

        private void LoadGstReport(string dateRange)
        {
            var res = DatabaseHelper.GetInvoicesPaged(search: null, customerId: 0, farmerId: 0, paymentMethod: "All", dateRange: dateRange, customStart: null, customEnd: null, page: 1, pageSize: 1000);
            var items = res.Items.Select((inv, idx) => new
            {
                No = idx + 1,
                InvoiceNo = inv.InvoiceNo,
                Date = inv.InvoiceDate.ToString("dd MMM yyyy"),
                FarmerName = string.IsNullOrWhiteSpace(inv.CustomerName) ? "Walk-in Farmer" : inv.CustomerName,
                TaxableAmount = $"₹ {inv.SubTotal:N2}",
                CGST = $"₹ {(inv.GstAmount / 2m):N2}",
                SGST = $"₹ {(inv.GstAmount / 2m):N2}",
                TotalGST = $"₹ {inv.GstAmount:N2}",
                InvoiceValue = $"₹ {inv.GrandTotal:N2}"
            }).ToList();

            _allItems = items.Cast<object>().ToList();
            _currentPage = 1;
            RenderPage();

            SetStat(1, "🧾", $"Total Invoices ({dateRange})", res.Total.ToString(), "Total GST invoices");
            SetStat(2, "₹", "Taxable Sales", $"₹ {res.TotalSubTotal:N2}", "Total taxable amount");
            SetStat(3, "📑", "Total GST", $"₹ {res.TotalGst:N2}", "CGST + SGST combined");
            SetStat(4, "➗", "CGST / SGST", $"₹ {(res.TotalGst / 2m):N2}", "Each half of total GST");

            TxtReportSummary.Text = $"GST Tax Report ({dateRange}): Taxable Sales = ₹ {res.TotalSubTotal:N2} | Total GST = ₹ {res.TotalGst:N2}";
        }

        private void LoadPurchaseReport(string dateRange)
        {
            var resPurchases = DatabaseHelper.GetPurchasesPaged(null, 1, 1000);
            var resReturns = DatabaseHelper.GetPurchaseReturnsPaged(null, 1, 1000);

            decimal totalPurchaseEntryAmount = resPurchases.Items.Sum(p => p.GrandTotal);
            decimal totalPurchaseReturnAmount = resReturns.Items.Sum(r => r.GrandTotal);
            decimal netPurchaseAmount = totalPurchaseEntryAmount - totalPurchaseReturnAmount;
            decimal totalSupplierPayable = resPurchases.Items.Sum(p => p.PayableAmount);

            var items = resPurchases.Items.Select((p, idx) =>
            {
                decimal returnAmt = resReturns.Items.Where(r => r.PurchaseId == p.PurchaseId).Sum(r => r.GrandTotal);
                return new
                {
                    No = idx + 1,
                    PurchaseNo = p.PurchaseNumber,
                    Supplier = p.SupplierName,
                    InvoiceNo = p.SupplierInvoiceNumber,
                    Date = p.PurchaseDate.ToString("dd MMM yyyy"),
                    PurchaseAmount = $"₹ {p.GrandTotal:N2}",
                    ReturnAmount = $"₹ {returnAmt:N2}",
                    NetAmount = $"₹ {(p.GrandTotal - returnAmt):N2}",
                    PayableAmount = $"₹ {p.PayableAmount:N2}"
                };
            }).ToList();

            _allItems = items.Cast<object>().ToList();
            _currentPage = 1;
            RenderPage();

            SetStat(1, "🛍️", $"Total Purchases ({dateRange})", resPurchases.Total.ToString(), "Purchase entries");
            SetStat(2, "₹", "Total Purchase Amount", $"₹ {totalPurchaseEntryAmount:N2}", "Gross purchase value");
            SetStat(3, "↩️", "Total Returns", $"₹ {totalPurchaseReturnAmount:N2}", "Purchase return value");
            SetStat(4, "💰", "Total Payable", $"₹ {totalSupplierPayable:N2}", "Amount due to suppliers");

            TxtReportSummary.Text = $"Purchase & Supplier Report ({dateRange}): {resPurchases.Total} Purchases | Total Purchase Entry Amount: ₹ {totalPurchaseEntryAmount:N2} | Total Purchase Return Amount: ₹ {totalPurchaseReturnAmount:N2} | Net Amount: ₹ {netPurchaseAmount:N2} | Total Supplier Payable: ₹ {totalSupplierPayable:N2}";
        }

        private string GetCurrentTabTitle()
        {
            // Prefer dropdown selector if present
            var selTag = (CmbReportSelector?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(selTag))
            {
                switch (selTag.ToLowerInvariant())
                {
                    case "sales": return "Sales & Billing Report";
                    case "stock": return "Stock Valuation Report";
                    case "udhar": return "Udhar Balance Report";
                    case "gst": return "GST Tax Report";
                    case "purchase": return "Purchase & Supplier Report";
                }
            }

            // Fallback default
            return "Sales & Billing Report";
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allItems == null || _allItems.Count == 0)
                {
                    MessageBox.Show("No report data available to export.", "Export Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string tabTitle = GetCurrentTabTitle();
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"{tabTitle.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    System.Reflection.PropertyInfo[] props = null;
                    foreach (var item in _allItems)
                    {
                        if (props == null)
                        {
                            props = item.GetType().GetProperties();
                            sb.AppendLine(string.Join(",", props.Select(p => $"\"{p.Name}\"")));
                        }
                        var rowVals = props.Select(p =>
                        {
                            var val = p.GetValue(item)?.ToString() ?? "";
                            return $"\"{val.Replace("\"", "\"\"")}\"";
                        });
                        sb.AppendLine(string.Join(",", rowVals));
                    }

                    File.WriteAllText(saveDlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Report exported successfully to CSV:\n{saveDlg.FileName}", "Export CSV Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allItems == null || _allItems.Count == 0)
                {
                    MessageBox.Show("No report data available to save as PDF.", "Save PDF Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string tabTitle = GetCurrentTabTitle();
                var saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF Documents (*.pdf)|*.pdf",
                    FileName = $"{tabTitle.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                };

                if (saveDlg.ShowDialog() == true)
                {
                    ReportPdfHelper.GenerateReportPdf(tabTitle, TxtReportSummary.Text, _allItems, saveDlg.FileName);
                    MessageBox.Show($"Report PDF saved successfully:\n{saveDlg.FileName}", "Save PDF Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving PDF: {ex.Message}", "Save PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allItems == null || _allItems.Count == 0)
                {
                    MessageBox.Show("No report data available to print.", "Print Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string tempFolder = Path.GetTempPath();
                string tempPdf = Path.Combine(tempFolder, $"Report_Print_{Guid.NewGuid():N}.pdf");

                string tabTitle = GetCurrentTabTitle();
                ReportPdfHelper.GenerateReportPdf(tabTitle, TxtReportSummary.Text, _allItems, tempPdf);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPdf) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing report: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}