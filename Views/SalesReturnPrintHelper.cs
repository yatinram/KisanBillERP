using System;
using System.IO;
using KrushiBillERP.Data;
using KrushiBillERP.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace KrushiBillERP.Views
{
    public static class SalesReturnPrintHelper
    {
        private static readonly XColor PrimaryGreen = XColor.FromArgb(27, 94, 32);   // #1B5E20
        private static readonly XColor TextDark = XColor.FromArgb(30, 36, 34);      // #1E2422
        private static readonly XColor TextMuted = XColor.FromArgb(108, 117, 125);  // #6C757D
        private static readonly XColor BorderColor = XColor.FromArgb(226, 232, 240); // #E2E8F0
        private static readonly XColor LightBg = XColor.FromArgb(248, 250, 249);     // #F8FAF9

        public static string GeneratePdf(int salesReturnId, string outputPath = null)
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new SimpleFontResolver();

            var returnHeader = DatabaseHelper.GetSalesReturnById(salesReturnId);
            var items = DatabaseHelper.GetSalesReturnItems(salesReturnId);
            var settings = DatabaseHelper.GetCompanySettings();

            if (returnHeader == null) throw new Exception("Sales return record not found.");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string tempDir = Path.GetTempPath();
                outputPath = Path.Combine(tempDir, $"Sales_Return_{salesReturnId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            using var doc = new PdfDocument();
            doc.Info.Title = $"Sales Return {returnHeader.ReturnNumber}";

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
            gfx.DrawString("SALES RETURN VOUCHER", Bold(14), XBrushes.White,
                new XRect(x, 14, contentW, 24), XStringFormats.TopRight);

            if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
            {
                gfx.DrawString(settings.ShopAddress, Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopLeft);
            }
            if (!string.IsNullOrWhiteSpace(settings?.GSTIN))
            {
                gfx.DrawString($"GSTIN: {settings.GSTIN}", Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopRight);
            }

            y = 82;

            // ── METADATA BOX ─────────────────────────────────────────────
            gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, 64);
            gfx.DrawRectangle(new XPen(BorderColor, 1), x, y, contentW, 64);

            double col1 = x + 10;
            double col2 = x + contentW / 2 + 10;

            gfx.DrawString($"Return No: {returnHeader.ReturnNumber}", Bold(10), new XSolidBrush(TextDark), new XRect(col1, y + 8, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Return Date: {returnHeader.ReturnDate:dd MMM yyyy}", Regular(9), new XSolidBrush(TextDark), new XRect(col2, y + 8, contentW / 2 - 15, 14), XStringFormats.TopLeft);

            gfx.DrawString($"Farmer Name: {returnHeader.FarmerName}", Bold(10), new XSolidBrush(PrimaryGreen), new XRect(col1, y + 26, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Original Bill No: {returnHeader.InvoiceNo}", Regular(9), new XSolidBrush(TextDark), new XRect(col2, y + 26, contentW / 2 - 15, 14), XStringFormats.TopLeft);

            gfx.DrawString($"Adjustment: {returnHeader.AdjustmentType}", Regular(9), new XSolidBrush(TextMuted), new XRect(col1, y + 44, contentW / 2 - 15, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Return Reason: {returnHeader.ReturnReason}", Regular(9), new XSolidBrush(TextMuted), new XRect(col2, y + 44, contentW / 2 - 15, 14), XStringFormats.TopLeft);

            y += 76;

            // ── ITEMS TABLE ──────────────────────────────────────────────
            // Items Table — column widths as % of contentW (sum = 100%, no overflow)
            double[] colWidths = {
                contentW * 0.04,  // #
                contentW * 0.30,  // Product Name
                contentW * 0.15,  // Batch
                contentW * 0.12,  // Expiry
                contentW * 0.12,  // Return Qty
                contentW * 0.13,  // Rate
                contentW * 0.14   // Amount
            };
            string[] headers = { "#", "Product Name", "Batch", "Expiry", "Return Qty", "Rate", "Amount" };

            const double headerH = 24;
            const double rowH    = 22;

            gfx.DrawRectangle(new XSolidBrush(PrimaryGreen), x, y, contentW, headerH);
            double curX = x;
            for (int i = 0; i < headers.Length; i++)
            {
                var align = (i >= 4) ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                gfx.DrawString(headers[i], Bold(9), XBrushes.White,
                    new XRect(curX + 5, y, colWidths[i] - 10, headerH), align);
                curX += colWidths[i];
                if (i < headers.Length - 1)
                    gfx.DrawLine(new XPen(XColors.White, 0.4), curX, y, curX, y + headerH);
            }
            y += headerH;

            int idx = 1;
            foreach (var item in items)
            {
                bool isAlt = (idx % 2 == 0);
                if (isAlt) gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, rowH);

                curX = x;
                string expStr = item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("dd/MM/yy") : "-";

                var cells = new (string val, bool right)[]
                {
                    (idx.ToString(),                    false),
                    (item.ProductName ?? "",             false),
                    (item.BatchNumber ?? "",             false),
                    (expStr,                             false),
                    (item.ReturnQuantity.ToString(),     true),
                    ($"₹{item.Rate:N2}",                true),
                    ($"₹{item.Amount:N2}",              true),
                };
                var fonts = new XFont[]
                {
                    Regular(9), Bold(9), Regular(8), Regular(8), Bold(9), Regular(8), Bold(9)
                };

                for (int ci = 0; ci < cells.Length; ci++)
                {
                    var algn = cells[ci].right ? XStringFormats.CenterRight : XStringFormats.CenterLeft;
                    gfx.DrawString(cells[ci].val, fonts[ci], new XSolidBrush(TextDark),
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

            // ── SUMMARY BOX ──────────────────────────────────────────────
            double sumW = 220;
            double sumX = x + contentW - sumW;
            gfx.DrawRectangle(new XSolidBrush(LightBg), sumX, y, sumW, 70);
            gfx.DrawRectangle(new XPen(BorderColor, 1), sumX, y, sumW, 70);

            double sy = y + 6;
            void DrawSumRow(string label, string val, bool isBold = false)
            {
                var font = isBold ? Bold(10) : Regular(9);
                var brush = isBold ? new XSolidBrush(PrimaryGreen) : new XSolidBrush(TextDark);
                gfx.DrawString(label, font, brush, new XRect(sumX + 10, sy, sumW / 2, 14), XStringFormats.TopLeft);
                gfx.DrawString(val, font, brush, new XRect(sumX + sumW / 2, sy, sumW / 2 - 10, 14), XStringFormats.TopRight);
                sy += 15;
            }

            DrawSumRow("Sub Total:", $"₹{returnHeader.SubTotal:N2}");
            DrawSumRow("GST Amount:", $"₹{returnHeader.GSTAmount:N2}");
            DrawSumRow("Return Total:", $"₹{returnHeader.GrandTotal:N2}", isBold: true);

            // Signature
            double signY = y + 80;
            gfx.DrawString("_______________________", Regular(9), new XSolidBrush(TextMuted), new XRect(x + contentW - 160, signY, 150, 14), XStringFormats.TopCenter);
            gfx.DrawString("Authorised Signatory", Bold(9), new XSolidBrush(TextDark), new XRect(x + contentW - 160, signY + 16, 150, 14), XStringFormats.TopCenter);

            doc.Save(outputPath);
            return outputPath;
        }

        private static XFont Regular(double size) => new XFont("Arial", size, XFontStyleEx.Regular);
        private static XFont Bold(double size) => new XFont("Arial", size, XFontStyleEx.Bold);
    }
}
