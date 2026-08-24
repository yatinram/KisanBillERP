using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class InvoiceDetailsWindow : Window
    {
        private int _invoiceId;

        public InvoiceDetailsWindow(int invoiceId)
        {
            InitializeComponent();
            _invoiceId = invoiceId;
            LoadInvoice(invoiceId);
        }

        private void LoadInvoice(int invoiceId)
        {
            try
            {
                var invoice = DatabaseHelper.GetInvoiceById(invoiceId);
                if (invoice != null)
                {
                    TxtInvoiceNo.Text = $"Invoice #{invoice.InvoiceNo}";
                    TxtDate.Text = $"Date: {invoice.InvoiceDate:dd MMM yyyy, hh:mm tt}";

                    TxtCustomerName.Text = string.IsNullOrWhiteSpace(invoice.CustomerName) ? "Walk-in Customer" : invoice.CustomerName;
                    TxtCustomerPhone.Text = string.Empty;

                    TxtPaymentMethod.Text = $"Payment: {invoice.PaymentMethod}";

                    TxtSubTotal.Text = $"₹ {invoice.SubTotal:N2}";
                    TxtGstTotal.Text = $"₹ {invoice.GstAmount:N2}";
                    TxtGrandTotal.Text = $"₹ {invoice.GrandTotal:N2}";

                    var items = DatabaseHelper.GetInvoiceItems(invoiceId);
                    GridItems.ItemsSource = items;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading invoice details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static void SimulateCtrlV()
        {
            try
            {
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        private void BtnWhatsApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var invoice  = DatabaseHelper.GetInvoiceById(_invoiceId);
                var items    = DatabaseHelper.GetInvoiceItems(_invoiceId);
                var settings = DatabaseHelper.GetCompanySettings();

                // ── 1. Generate PDF Invoice ───────────────────────────────
                BtnWhatsApp.IsEnabled = false;
                BtnWhatsApp.Content   = "⏳ Generating PDF...";

                string pdfPath = null;
                try
                {
                    pdfPath = InvoicePdfHelper.GeneratePdf(_invoiceId);
                }
                catch (Exception pdfEx)
                {
                    MessageBox.Show($"PDF generation notice: {pdfEx.Message}",
                        "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally
                {
                    BtnWhatsApp.IsEnabled = true;
                    BtnWhatsApp.Content   = "💬 Send via WhatsApp";
                }

                // ── 2. Put PDF file on Clipboard for 1-click Ctrl+V in WhatsApp ──
                bool pdfCopied = false;
                if (!string.IsNullOrEmpty(pdfPath) && System.IO.File.Exists(pdfPath))
                {
                    try
                    {
                        var fileDrop = new System.Collections.Specialized.StringCollection { pdfPath };
                        Clipboard.SetFileDropList(fileDrop);
                        pdfCopied = true;
                    }
                    catch { }
                }

                // ── 3. Open WhatsApp App (or Web) without text message ───────
                string phone = invoice.MobileNumber ?? "";
                phone = phone.Replace(" ", "").Replace("-", "").Replace("+", "");
                if (phone.StartsWith("91") && phone.Length == 12) phone = phone.Substring(2);
                if (phone.Length == 10) phone = "91" + phone;

                bool hasPhone = !string.IsNullOrWhiteSpace(phone) && phone.Length >= 12;

                string appUri = hasPhone ? $"whatsapp://send?phone={phone}" : "whatsapp://";
                string webUrl = hasPhone ? $"https://web.whatsapp.com/send?phone={phone}" : "https://web.whatsapp.com/";

                try
                {
                    Process.Start(new ProcessStartInfo(appUri) { UseShellExecute = true });
                }
                catch
                {
                    Process.Start(new ProcessStartInfo(webUrl) { UseShellExecute = true });
                }

                // ── 5. Auto-attach PDF by simulating Ctrl+V after WhatsApp opens ──
                if (pdfCopied)
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(2000);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SimulateCtrlV();
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WhatsApp error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            PrintDirectly(_invoiceId);
        }

        public static void PrintDirectly(int invoiceId)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var invoice = DatabaseHelper.GetInvoiceById(invoiceId);
                    var items = DatabaseHelper.GetInvoiceItems(invoiceId);
                    var settings = DatabaseHelper.GetCompanySettings();

                    FlowDocument doc = new FlowDocument();
                    doc.PagePadding = new Thickness(40);
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;

                    doc.Blocks.Add(new Paragraph(new Run(settings?.ShopName ?? "KrushiBill ERP"))
                    {
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center
                    });

                    if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
                    {
                        doc.Blocks.Add(new Paragraph(new Run(settings.ShopAddress))
                        {
                            FontSize = 11,
                            TextAlignment = TextAlignment.Center
                        });
                    }

                    doc.Blocks.Add(new Paragraph(new Run("TAX INVOICE"))
                    {
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 15)
                    });

                    doc.Blocks.Add(new Paragraph(new Run($"Invoice No: {invoice.InvoiceNo}    Date: {invoice.InvoiceDate:dd MMM yyyy, hh:mm tt}")) { FontSize = 12, FontWeight = FontWeights.SemiBold });
                    doc.Blocks.Add(new Paragraph(new Run($"Customer: {invoice.CustomerName}    Payment Mode: {invoice.PaymentMethod}")) { Margin = new Thickness(0, 0, 0, 15) });

                    Table table = new Table { CellSpacing = 0, BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 1) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(60) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(90) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(70) });
                    table.Columns.Add(new TableColumn { Width = new GridLength(100) });

                    TableRowGroup rg = new TableRowGroup();
                    TableRow header = new TableRow { Background = System.Windows.Media.Brushes.LightGray };
                    header.Cells.Add(new TableCell(new Paragraph(new Run("Product Name")) { FontWeight = FontWeights.Bold }));
                    header.Cells.Add(new TableCell(new Paragraph(new Run("Qty")) { FontWeight = FontWeights.Bold }));
                    header.Cells.Add(new TableCell(new Paragraph(new Run("Rate")) { FontWeight = FontWeights.Bold }));
                    header.Cells.Add(new TableCell(new Paragraph(new Run("GST %")) { FontWeight = FontWeights.Bold }));
                    header.Cells.Add(new TableCell(new Paragraph(new Run("Amount")) { FontWeight = FontWeights.Bold }));
                    rg.Rows.Add(header);

                    foreach (var item in items)
                    {
                        TableRow row = new TableRow();
                        row.Cells.Add(new TableCell(new Paragraph(new Run(item.ProductName))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run(item.Qty.ToString()))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {item.Rate:N2}"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"{item.GstPercent}%"))));
                        row.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {item.Amount:N2}")) { FontWeight = FontWeights.SemiBold }));
                        rg.Rows.Add(row);
                    }
                    table.RowGroups.Add(rg);
                    doc.Blocks.Add(table);

                    doc.Blocks.Add(new Paragraph(new Run($"SubTotal: ₹ {invoice.SubTotal:N2}"))
                    {
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 15, 0, 4)
                    });
                    doc.Blocks.Add(new Paragraph(new Run($"GST Total: ₹ {invoice.GstAmount:N2}"))
                    {
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    doc.Blocks.Add(new Paragraph(new Run($"Grand Total: ₹ {invoice.GrandTotal:N2}"))
                    {
                        TextAlignment = TextAlignment.Right,
                        FontWeight = FontWeights.Bold,
                        FontSize = 16
                    });

                    if (!string.IsNullOrWhiteSpace(settings?.TermsAndConditions))
                    {
                        doc.Blocks.Add(new Paragraph(new Run($"Terms:\n{settings.TermsAndConditions}"))
                        {
                            FontSize = 10,
                            Foreground = System.Windows.Media.Brushes.Gray,
                            Margin = new Thickness(0, 20, 0, 0)
                        });
                    }

                    printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Tax Invoice - {invoice.InvoiceNo}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing invoice: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
