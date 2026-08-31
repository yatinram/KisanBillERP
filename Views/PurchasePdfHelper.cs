using System;
using System.IO;
using KrushiBillERP.Data;
using KrushiBillERP.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Fonts;

namespace KrushiBillERP.Views
{
    public static class PurchasePdfHelper
    {
        private static readonly XColor PrimaryGreen = XColor.FromArgb(27, 94, 32);  // #1B5E20
        private static readonly XColor TextDark = XColor.FromArgb(30, 36, 34);     // #1E2422
        private static readonly XColor TextMuted = XColor.FromArgb(108, 117, 125); // #6C757D
        private static readonly XColor BorderColor = XColor.FromArgb(226, 232, 240); // #E2E8F0
        private static readonly XColor LightBg = XColor.FromArgb(248, 250, 249);    // #F8FAF9

        public static string GeneratePdf(int purchaseId, string outputPath = null)
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new SimpleFontResolver();

            var purchase = DatabaseHelper.GetPurchaseById(purchaseId);
            var items = DatabaseHelper.GetPurchaseItems(purchaseId);
            var settings = DatabaseHelper.GetCompanySettings();

            if (purchase == null) throw new Exception("Purchase record not found.");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string tempDir = Path.GetTempPath();
                outputPath = Path.Combine(tempDir, $"Purchase_Voucher_{purchaseId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            using var doc = new PdfDocument();
            doc.Info.Title = $"Purchase Voucher {purchase.PurchaseNumber}";

            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(210); // A4
            page.Height = XUnit.FromMillimeter(297);

            using var gfx = XGraphics.FromPdfPage(page);

            double margin = 36;
            double pageW = page.Width.Point;
            double x = margin;
            double y = margin;
            double contentW = pageW - margin * 2;

            // ── HEADER BAND ──────────────────────────────────────────────
            gfx.DrawRectangle(new XSolidBrush(PrimaryGreen), x - margin, 0, pageW, 70);

            var shopName = settings?.ShopName ?? "KRUSHI KENDRA AGRICULTURE & PESTICIDES";
            gfx.DrawString(shopName, Bold(15), XBrushes.White,
                new XRect(x, 14, contentW, 24), XStringFormats.TopLeft);

            if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
            {
                gfx.DrawString(settings.ShopAddress, Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopLeft);
            }
            if (!string.IsNullOrWhiteSpace(settings?.ShopPhone))
            {
                gfx.DrawString($"Phone: {settings.ShopPhone}", Regular(9), XBrushes.White,
                    new XRect(x, 48, contentW, 14), XStringFormats.TopLeft);
            }

            // Right header title
            gfx.DrawString("PURCHASE VOUCHER", Bold(14), XBrushes.White,
                new XRect(x, 14, contentW, 24), XStringFormats.TopRight);
            if (!string.IsNullOrWhiteSpace(settings?.GSTIN))
            {
                gfx.DrawString($"GSTIN: {settings.GSTIN}", Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopRight);
            }

            y = 82;

            // ── PURCHASE METADATA ──────────────────────────────────────────
            gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, 64);
            gfx.DrawRectangle(new XPen(BorderColor, 1), x, y, contentW, 64);

            double col1 = x + 10;
            double col2 = x + contentW / 2 + 10;

            gfx.DrawString($"Purchase No: {purchase.PurchaseNumber}", Bold(10), new XSolidBrush(TextDark), new XRect(col1, y + 8, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Purchase Date: {purchase.PurchaseDate:dd MMM yyyy}", Regular(9), new XSolidBrush(TextDark), new XRect(col2, y + 8, contentW / 2 - 15, 14), XStringFormats.TopLeft);

            gfx.DrawString($"Supplier: {purchase.SupplierName}", Bold(10), new XSolidBrush(PrimaryGreen), new XRect(col1, y + 26, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Supplier Invoice No: {purchase.SupplierInvoiceNumber}", Regular(9), new XSolidBrush(TextDark), new XRect(col2, y + 26, contentW / 2 - 15, 14), XStringFormats.TopLeft);

            gfx.DrawString($"Payment Mode: {purchase.PaymentMethod}", Regular(9), new XSolidBrush(TextMuted), new XRect(col1, y + 44, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            if (!string.IsNullOrWhiteSpace(purchase.PaymentReference))
            {
                gfx.DrawString($"Ref: {purchase.PaymentReference}", Regular(9), new XSolidBrush(TextMuted), new XRect(col2, y + 44, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            }

            y += 76;

            // ── ITEMS TABLE ───────────────────────────────────────────────
            // Column widths as % of contentW — sum = 100%, no overflow ever
            double[] colWidths = {
                contentW * 0.04,  // #
                contentW * 0.26,  // Product
                contentW * 0.14,  // Company
                contentW * 0.11,  // Batch
                contentW * 0.11,  // Expiry
                contentW * 0.08,  // Qty
                contentW * 0.12,  // Price
                contentW * 0.07,  // GST
                contentW * 0.07   // Amount
            };
            string[] headers = { "#", "Product", "Company", "Batch", "Expiry", "Qty", "Price", "GST", "Amount" };

            const double headerH = 24;
            const double rowH    = 22;

            // Draw Header Bar
            gfx.DrawRectangle(new XSolidBrush(PrimaryGreen), x, y, contentW, headerH);
            double curX = x;
            for (int i = 0; i < headers.Length; i++)
            {
                var align = (i >= 5) ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                gfx.DrawString(headers[i], Bold(9), XBrushes.White,
                    new XRect(curX + 5, y, colWidths[i] - 10, headerH), align);
                curX += colWidths[i];
                if (i < headers.Length - 1)
                    gfx.DrawLine(new XPen(XColors.White, 0.4), curX, y, curX, y + headerH);
            }
            y += headerH;

            // Draw Item Rows
            int idx = 1;
            foreach (var item in items)
            {
                bool isAlt = (idx % 2 == 0);
                if (isAlt)
                    gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, rowH);

                curX = x;
                string qtyStr = item.FreeQuantity > 0 ? $"{item.Quantity}+{item.FreeQuantity}" : item.Quantity.ToString();
                string expStr = item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("dd/MM/yy") : "-";

                // Draw each cell manually
                var cells = new (string val, XFont fnt, bool right)[]
                {
                    (idx.ToString(),                    Regular(9),  false),
                    (item.ProductName ?? "",            Bold(9),     false),
                    (item.Company ?? "",                Regular(8),  false),
                    (item.BatchNumber ?? "",            Regular(8),  false),
                    (expStr,                            Regular(8),  false),
                    (qtyStr,                            Regular(9),  true),
                    ($"₹{item.PurchasePrice:N2}",       Regular(8),  true),
                    ($"{item.GST}%",                    Regular(8),  true),
                    ($"₹{item.Amount:N2}",              Bold(9),     true),
                };

                curX = x;
                for (int ci = 0; ci < cells.Length; ci++)
                {
                    var (val, fnt, right) = cells[ci];
                    var algn = right ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                    gfx.DrawString(val, fnt, new XSolidBrush(ci == 7 ? TextMuted : TextDark),
                        new XRect(curX + 5, y, colWidths[ci] - 10, rowH), algn);
                    curX += colWidths[ci];
                    if (ci < cells.Length - 1)
                        gfx.DrawLine(new XPen(BorderColor, 0.3), curX, y, curX, y + rowH);
                }

                gfx.DrawLine(new XPen(BorderColor, 0.3), x, y + rowH, x + contentW, y + rowH);
                y += rowH;
                idx++;
            }

            gfx.DrawLine(new XPen(BorderColor, 1), x, y, x + contentW, y);
            y += 12;

            // ── SUMMARY & TOTALS ──────────────────────────────────────────
            double sumW = 220;
            double sumX = x + contentW - sumW;

            gfx.DrawRectangle(new XSolidBrush(LightBg), sumX, y, sumW, 100);
            gfx.DrawRectangle(new XPen(BorderColor, 1), sumX, y, sumW, 100);

            double sy = y + 6;
            void DrawSumRow(string label, string val, bool isBold = false)
            {
                var font = isBold ? Bold(10) : Regular(9);
                var brush = isBold ? new XSolidBrush(PrimaryGreen) : new XSolidBrush(TextDark);
                gfx.DrawString(label, font, brush, new XRect(sumX + 10, sy, sumW / 2, 14), XStringFormats.TopLeft);
                gfx.DrawString(val, font, brush, new XRect(sumX + sumW / 2, sy, sumW / 2 - 10, 14), XStringFormats.TopRight);
                sy += 15;
            }

            DrawSumRow("Sub Total:", $"₹{purchase.SubTotal:N2}");
            DrawSumRow("Discount:", $"₹{purchase.Discount:N2}");
            DrawSumRow("GST Amount:", $"₹{purchase.GSTAmount:N2}");
            DrawSumRow("Grand Total:", $"₹{purchase.GrandTotal:N2}", isBold: true);
            DrawSumRow("Paid Amount:", $"₹{purchase.PaidAmount:N2}");
            DrawSumRow("Balance Payable:", $"₹{purchase.PayableAmount:N2}", isBold: true);

            // Signature
            double signY = y + 110;
            gfx.DrawString("_______________________", Regular(9), new XSolidBrush(TextMuted), new XRect(x + contentW - 160, signY, 150, 14), XStringFormats.TopCenter);
            gfx.DrawString("Authorised Signatory", Bold(9), new XSolidBrush(TextDark), new XRect(x + contentW - 160, signY + 16, 150, 14), XStringFormats.TopCenter);

            doc.Save(outputPath);
            return outputPath;
        }

        private static XFont Regular(double size) => new XFont("Arial", size, XFontStyleEx.Regular);
        private static XFont Bold(double size) => new XFont("Arial", size, XFontStyleEx.Bold);
    }
}
