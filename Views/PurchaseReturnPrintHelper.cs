using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public static class PurchaseReturnPrintHelper
    {
        public static string GeneratePdf(int purchaseReturnId, string outputPath = null)
        {
            if (PdfSharp.Fonts.GlobalFontSettings.FontResolver == null)
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new SimpleFontResolver();

            var returnHeader = DatabaseHelper.GetPurchaseReturnById(purchaseReturnId);
            var items = DatabaseHelper.GetPurchaseReturnItems(purchaseReturnId);
            var settings = DatabaseHelper.GetCompanySettings();

            if (returnHeader == null) throw new Exception("Purchase return record not found.");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string tempDir = System.IO.Path.GetTempPath();
                outputPath = System.IO.Path.Combine(tempDir, $"Purchase_Return_{purchaseReturnId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            using var doc = new PdfSharp.Pdf.PdfDocument();
            doc.Info.Title = $"Purchase Return {returnHeader.ReturnNumber}";

            var page = doc.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromMillimeter(210);
            page.Height = PdfSharp.Drawing.XUnit.FromMillimeter(297);

            using var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

            double margin = 36;
            double pageW = page.Width.Point;
            double x = margin;
            double y = margin;
            double contentW = pageW - margin * 2;

            var darkGreen = PdfSharp.Drawing.XColor.FromArgb(27, 94, 32);
            var textDark = PdfSharp.Drawing.XColor.FromArgb(30, 36, 34);
            var textMuted = PdfSharp.Drawing.XColor.FromArgb(108, 117, 125);
            var borderColor = PdfSharp.Drawing.XColor.FromArgb(226, 232, 240);
            var lightBg = PdfSharp.Drawing.XColor.FromArgb(248, 250, 249);

            PdfSharp.Drawing.XFont Regular(double sz) => new PdfSharp.Drawing.XFont("Arial", sz, PdfSharp.Drawing.XFontStyleEx.Regular);
            PdfSharp.Drawing.XFont Bold(double sz) => new PdfSharp.Drawing.XFont("Arial", sz, PdfSharp.Drawing.XFontStyleEx.Bold);

            // Header Band
            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(darkGreen), x - margin, 0, pageW, 70);
            var shopName = settings?.ShopName ?? "KRUSHI KENDRA AGRICULTURE & PESTICIDES";
            gfx.DrawString(shopName, Bold(15), PdfSharp.Drawing.XBrushes.White, new PdfSharp.Drawing.XRect(x, 14, contentW, 24), PdfSharp.Drawing.XStringFormats.TopLeft);
            gfx.DrawString("PURCHASE RETURN VOUCHER", Bold(14), PdfSharp.Drawing.XBrushes.White, new PdfSharp.Drawing.XRect(x, 14, contentW, 24), PdfSharp.Drawing.XStringFormats.TopRight);

            if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
                gfx.DrawString(settings.ShopAddress, Regular(9), PdfSharp.Drawing.XBrushes.White, new PdfSharp.Drawing.XRect(x, 36, contentW, 14), PdfSharp.Drawing.XStringFormats.TopLeft);

            y = 82;

            // Metadata Box
            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(lightBg), x, y, contentW, 64);
            gfx.DrawRectangle(new PdfSharp.Drawing.XPen(borderColor, 1), x, y, contentW, 64);

            double col1 = x + 10;
            double col2 = x + contentW / 2 + 10;

            gfx.DrawString($"Return No: {returnHeader.ReturnNumber}", Bold(10), new PdfSharp.Drawing.XSolidBrush(textDark), new PdfSharp.Drawing.XRect(col1, y + 8, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);
            gfx.DrawString($"Return Date: {returnHeader.ReturnDate:dd MMM yyyy}", Regular(9), new PdfSharp.Drawing.XSolidBrush(textDark), new PdfSharp.Drawing.XRect(col2, y + 8, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);

            gfx.DrawString($"Supplier: {returnHeader.SupplierName}", Bold(10), new PdfSharp.Drawing.XSolidBrush(darkGreen), new PdfSharp.Drawing.XRect(col1, y + 26, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);
            gfx.DrawString($"Original Purchase No: {returnHeader.PurchaseNumber}", Regular(9), new PdfSharp.Drawing.XSolidBrush(textDark), new PdfSharp.Drawing.XRect(col2, y + 26, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);

            gfx.DrawString($"Supplier Invoice: {returnHeader.SupplierInvoiceNumber}", Regular(9), new PdfSharp.Drawing.XSolidBrush(textMuted), new PdfSharp.Drawing.XRect(col1, y + 44, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);
            gfx.DrawString($"Return Reason: {returnHeader.ReturnReason}", Regular(9), new PdfSharp.Drawing.XSolidBrush(textMuted), new PdfSharp.Drawing.XRect(col2, y + 44, contentW / 2 - 15, 14), PdfSharp.Drawing.XStringFormats.TopLeft);

            y += 76;

            // Items Table — column widths as % of contentW (sum = 100%, no overflow)
            double[] colWidths = {
                contentW * 0.04,  // #
                contentW * 0.30,  // Product Name
                contentW * 0.15,  // Batch
                contentW * 0.12,  // Expiry
                contentW * 0.12,  // Return Qty
                contentW * 0.13,  // Price
                contentW * 0.14   // Amount
            };
            string[] headers = { "#", "Product Name", "Batch", "Expiry", "Return Qty", "Price", "Amount" };

            const double headerH = 24;
            const double rowH    = 22;

            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(darkGreen), x, y, contentW, headerH);
            double curX = x;
            for (int i = 0; i < headers.Length; i++)
            {
                var align = (i >= 4) ? PdfSharp.Drawing.XStringFormats.CenterRight : PdfSharp.Drawing.XStringFormats.CenterLeft;
                gfx.DrawString(headers[i], Bold(9), PdfSharp.Drawing.XBrushes.White,
                    new PdfSharp.Drawing.XRect(curX + 5, y, colWidths[i] - 10, headerH), align);
                curX += colWidths[i];
                if (i < headers.Length - 1)
                    gfx.DrawLine(new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColors.White, 0.4), curX, y, curX, y + headerH);
            }
            y += headerH;

            int idx = 1;
            foreach (var item in items)
            {
                bool isAlt = (idx % 2 == 0);
                if (isAlt) gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(lightBg), x, y, contentW, rowH);

                curX = x;
                string expStr = item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("dd/MM/yy") : "-";

                var cells = new (string val, bool right)[]
                {
                    (idx.ToString(),                    false),
                    (item.ProductName ?? "",             false),
                    (item.BatchNumber ?? "",             false),
                    (expStr,                             false),
                    (item.ReturnQuantity.ToString(),     true),
                    ($"₹{item.PurchasePrice:N2}",        true),
                    ($"₹{item.Amount:N2}",               true),
                };

                var fonts = new PdfSharp.Drawing.XFont[]
                {
                    Regular(9), Bold(9), Regular(8), Regular(8), Bold(9), Regular(8), Bold(9)
                };

                for (int ci = 0; ci < cells.Length; ci++)
                {
                    var algn = cells[ci].right ? PdfSharp.Drawing.XStringFormats.CenterRight : PdfSharp.Drawing.XStringFormats.CenterLeft;
                    gfx.DrawString(cells[ci].val, fonts[ci], new PdfSharp.Drawing.XSolidBrush(textDark),
                        new PdfSharp.Drawing.XRect(curX + 5, y, colWidths[ci] - 10, rowH), algn);
                    curX += colWidths[ci];
                    if (ci < cells.Length - 1)
                        gfx.DrawLine(new PdfSharp.Drawing.XPen(borderColor, 0.3), curX, y, curX, y + rowH);
                }

                gfx.DrawLine(new PdfSharp.Drawing.XPen(borderColor, 0.3), x, y + rowH, x + contentW, y + rowH);
                y += rowH;
                idx++;
            }

            gfx.DrawLine(new PdfSharp.Drawing.XPen(borderColor, 1), x, y, x + contentW, y);
            y += 12;

            // Summary
            double sumW = 220;
            double sumX = x + contentW - sumW;
            gfx.DrawRectangle(new PdfSharp.Drawing.XSolidBrush(lightBg), sumX, y, sumW, 70);
            gfx.DrawRectangle(new PdfSharp.Drawing.XPen(borderColor, 1), sumX, y, sumW, 70);

            double sy = y + 6;
            void DrawSumRow(string label, string val, bool isBold = false)
            {
                var font = isBold ? Bold(10) : Regular(9);
                var brush = isBold ? new PdfSharp.Drawing.XSolidBrush(darkGreen) : new PdfSharp.Drawing.XSolidBrush(textDark);
                gfx.DrawString(label, font, brush, new PdfSharp.Drawing.XRect(sumX + 10, sy, sumW / 2, 14), PdfSharp.Drawing.XStringFormats.TopLeft);
                gfx.DrawString(val, font, brush, new PdfSharp.Drawing.XRect(sumX + sumW / 2, sy, sumW / 2 - 10, 14), PdfSharp.Drawing.XStringFormats.TopRight);
                sy += 15;
            }

            DrawSumRow("Sub Total:", $"₹{returnHeader.SubTotal:N2}");
            DrawSumRow("GST Amount:", $"₹{returnHeader.GSTAmount:N2}");
            DrawSumRow("Return Total:", $"₹{returnHeader.GrandTotal:N2}", isBold: true);

            // Signature
            double signY = y + 80;
            gfx.DrawString("_______________________", Regular(9), new PdfSharp.Drawing.XSolidBrush(textMuted), new PdfSharp.Drawing.XRect(x + contentW - 160, signY, 150, 14), PdfSharp.Drawing.XStringFormats.TopCenter);
            gfx.DrawString("Authorised Signatory", Bold(9), new PdfSharp.Drawing.XSolidBrush(textDark), new PdfSharp.Drawing.XRect(x + contentW - 160, signY + 16, 150, 14), PdfSharp.Drawing.XStringFormats.TopCenter);

            doc.Save(outputPath);
            return outputPath;
        }

        public static void PrintReturn(int purchaseReturnId)
        {
            var returnHeader = DatabaseHelper.GetPurchaseReturnById(purchaseReturnId);
            if (returnHeader == null)
            {
                MessageBox.Show("Purchase return record not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var items = DatabaseHelper.GetPurchaseReturnItems(purchaseReturnId);

            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                var doc = CreateFlowDocument(returnHeader, items);
                doc.PageWidth = printDlg.PrintableAreaWidth;
                doc.PageHeight = printDlg.PrintableAreaHeight;
                doc.PagePadding = new Thickness(40);
                doc.ColumnWidth = printDlg.PrintableAreaWidth;

                IDocumentPaginatorSource idpSource = doc;
                printDlg.PrintDocument(idpSource.DocumentPaginator, $"Purchase Return - {returnHeader.ReturnNumber}");
            }
        }

        private static FlowDocument CreateFlowDocument(PurchaseReturn rHeader, System.Collections.Generic.List<PurchaseReturnItem> items)
        {
            var settings = DatabaseHelper.GetCompanySettings();
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");

            // Header Section
            string shopName = settings?.ShopName ?? "KRUSHI KENDRA AGRICULTURE & PESTICIDES";
            var pShopName = new Paragraph(new Run(shopName))
            {
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B5E20")),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            doc.Blocks.Add(pShopName);

            var pTitle = new Paragraph(new Run("PURCHASE RETURN VOUCHER"))
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            doc.Blocks.Add(pTitle);

            // Metadata Grid / Table
            var metaTable = new Table();
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var metaGroup = new TableRowGroup();

            var r1 = new TableRow();
            r1.Cells.Add(new TableCell(new Paragraph(new Run($"Return No: {rHeader.ReturnNumber}")) { FontWeight = FontWeights.Bold }));
            r1.Cells.Add(new TableCell(new Paragraph(new Run($"Return Date: {rHeader.ReturnDate:dd MMM yyyy}"))));
            metaGroup.Rows.Add(r1);

            var r2 = new TableRow();
            r2.Cells.Add(new TableCell(new Paragraph(new Run($"Purchase No: {rHeader.PurchaseNumber}"))));
            r2.Cells.Add(new TableCell(new Paragraph(new Run($"Supplier Name: {rHeader.SupplierName}")) { FontWeight = FontWeights.Bold }));
            metaGroup.Rows.Add(r2);

            var r3 = new TableRow();
            r3.Cells.Add(new TableCell(new Paragraph(new Run($"Supplier Invoice: {rHeader.SupplierInvoiceNumber}"))));
            r3.Cells.Add(new TableCell(new Paragraph(new Run($"Paper Bill No: {rHeader.PaperBillNumber}"))));
            metaGroup.Rows.Add(r3);

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            // Spacer
            doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0, 10, 0, 10) });

            // Items Table
            var itemTable = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 1) };
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(40) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(90) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(100) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(60) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(80) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(60) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(90) });

            var itemGroup = new TableRowGroup();

            // Table Header
            var headRow = new TableRow { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAF9")) };
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("#")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Product")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Batch")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Expiry")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Return Qty")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Rate")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("GST")) { FontWeight = FontWeights.Bold }));
            headRow.Cells.Add(new TableCell(new Paragraph(new Run("Amount")) { FontWeight = FontWeights.Bold }));
            itemGroup.Rows.Add(headRow);

            int idx = 1;
            foreach (var it in items)
            {
                var row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(idx++.ToString()))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(it.ProductName))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(it.BatchNumber))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(it.ExpiryDate.HasValue ? it.ExpiryDate.Value.ToString("dd MMM yyyy") : "-"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run(it.ReturnQuantity.ToString()))));
                row.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {it.PurchasePrice:N2}"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run($"{it.GST}%"))));
                row.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {it.Amount:N2}")) { FontWeight = FontWeights.SemiBold }));
                itemGroup.Rows.Add(row);
            }

            itemTable.RowGroups.Add(itemGroup);
            doc.Blocks.Add(itemTable);

            // Summary Section
            var pReason = new Paragraph(new Run($"Return Reason: {rHeader.ReturnReason}")) { Margin = new Thickness(0, 14, 0, 4), FontWeight = FontWeights.Bold };
            doc.Blocks.Add(pReason);

            if (!string.IsNullOrWhiteSpace(rHeader.Notes))
            {
                var pNotes = new Paragraph(new Run($"Notes: {rHeader.Notes}")) { Margin = new Thickness(0, 0, 0, 10), Foreground = Brushes.Gray };
                doc.Blocks.Add(pNotes);
            }

            var pSummary = new Paragraph
            {
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            pSummary.Inlines.Add(new Run($"Subtotal: ₹ {rHeader.SubTotal:N2}\n"));
            pSummary.Inlines.Add(new Run($"Discount: ₹ {rHeader.Discount:N2}\n"));
            pSummary.Inlines.Add(new Run($"Taxable Amount: ₹ {rHeader.TaxableAmount:N2}\n"));
            pSummary.Inlines.Add(new Run($"GST Amount: ₹ {rHeader.GSTAmount:N2}\n"));
            pSummary.Inlines.Add(new Run($"Round Off: ₹ {rHeader.RoundOff:N2}\n"));
            pSummary.Inlines.Add(new Bold(new Run($"RETURN TOTAL: ₹ {rHeader.GrandTotal:N2}")) { FontSize = 14 });
            doc.Blocks.Add(pSummary);

            // Signatures
            var pSign = new Paragraph(new Run("\n\n_______________________\nAuthorised Signatory")) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 30, 0, 0) };
            doc.Blocks.Add(pSign);

            return doc;
        }
    }
}
