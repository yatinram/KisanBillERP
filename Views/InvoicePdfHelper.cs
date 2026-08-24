using System;
using System.Collections.Generic;
using System.IO;
using KrushiBillERP.Data;
using KrushiBillERP.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Fonts;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Threading;

namespace KrushiBillERP.Views
{
    public static class InvoicePdfHelper
    {
        // Green brand colour
        private static readonly XColor Green = XColor.FromArgb(5, 150, 105);   // #059669
        private static readonly XColor GreenDark = XColor.FromArgb(4, 120, 87); // #047857
        private static readonly XColor LightGreen = XColor.FromArgb(236, 253, 245);
        private static readonly XColor TextDark = XColor.FromArgb(15, 23, 42);  // #0F172A
        private static readonly XColor TextMuted = XColor.FromArgb(100, 116, 139); // #64748B
        private static readonly XColor BorderColor = XColor.FromArgb(226, 232, 240); // #E2E8F0
        private static readonly XColor RowAlt = XColor.FromArgb(248, 250, 252);     // #F8FAFC

        /// <summary>
        /// Generates an invoice PDF, saves it to the given path and returns the path.
        /// </summary>
        public static string GeneratePdf(int invoiceId, string outputPath = null)
        {
            // Ensure a font resolver is available so PdfSharp can find/embed fonts (prevents "No appropriate font found" errors)
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new SimpleFontResolver();

            var invoice = DatabaseHelper.GetInvoiceById(invoiceId);
            var items   = DatabaseHelper.GetInvoiceItems(invoiceId);
            var settings = DatabaseHelper.GetCompanySettings();

            if (invoice == null) throw new Exception("Invoice not found.");

            // Output path: Desktop if not specified
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string safe    = MakeSafeFilename(invoice.InvoiceNo);
                outputPath = Path.Combine(desktop, $"Invoice_{safe}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            using var doc  = new PdfDocument();
            doc.Info.Title = $"Invoice {invoice.InvoiceNo}";

            var page = doc.AddPage();
            page.Width  = XUnit.FromMillimeter(210); // A4
            page.Height = XUnit.FromMillimeter(297);

            using var gfx = XGraphics.FromPdfPage(page);

            double margin = 36;
            double pageW  = page.Width.Point;
            double x      = margin;
            double y      = margin;
            double contentW = pageW - margin * 2;

            // ── HEADER BAND ──────────────────────────────────────────────
            gfx.DrawRectangle(new XSolidBrush(Green), x - margin, 0, pageW, 70);

            var shopName = settings?.ShopName ?? "KrushiBill ERP";
            gfx.DrawString(shopName, Bold(16), XBrushes.White,
                new XRect(x, 14, contentW, 24), XStringFormats.TopLeft);

            if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
            {
                gfx.DrawString(settings.ShopAddress, Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopLeft);
            }
            if (!string.IsNullOrWhiteSpace(settings?.ShopPhone))
            {
                gfx.DrawString($"📞 {settings.ShopPhone}", Regular(9), XBrushes.White,
                    new XRect(x, 48, contentW, 14), XStringFormats.TopLeft);
            }

            // TAX INVOICE label (right side of header)
            gfx.DrawString("TAX INVOICE", Bold(14), XBrushes.White,
                new XRect(x, 14, contentW, 24), XStringFormats.TopRight);
            if (!string.IsNullOrWhiteSpace(settings?.GSTIN))
            {
                gfx.DrawString($"GSTIN: {settings.GSTIN}", Regular(9), XBrushes.White,
                    new XRect(x, 36, contentW, 14), XStringFormats.TopRight);
            }

            y = 86;

            // ── INVOICE META (two columns) ────────────────────────────────
            // Left: customer info
            gfx.DrawString("Bill To:", Bold(10), new XSolidBrush(TextMuted),
                new XRect(x, y, contentW / 2, 14), XStringFormats.TopLeft);
            y += 16;
            gfx.DrawString(invoice.CustomerName ?? "Walk-in Customer", Bold(12), new XSolidBrush(TextDark),
                new XRect(x, y, contentW / 2, 16), XStringFormats.TopLeft);
            y += 16;
            if (!string.IsNullOrWhiteSpace(invoice.MobileNumber))
            {
                gfx.DrawString($"📞 {invoice.MobileNumber}", Regular(10), new XSolidBrush(TextMuted),
                    new XRect(x, y, contentW / 2, 14), XStringFormats.TopLeft);
                y += 14;
            }
            if (!string.IsNullOrWhiteSpace(invoice.VillageName))
            {
                gfx.DrawString($"📍 {invoice.VillageName}", Regular(10), new XSolidBrush(TextMuted),
                    new XRect(x, y, contentW / 2, 14), XStringFormats.TopLeft);
            }

            // Right: invoice numbers & dates
            double infoY = 86;
            DrawLabelValue(gfx, x + contentW / 2, infoY, contentW / 2,
                "Invoice No.:", invoice.InvoiceNo ?? "");
            infoY += 18;
            if (!string.IsNullOrWhiteSpace(invoice.PaperBillNo))
            {
                DrawLabelValue(gfx, x + contentW / 2, infoY, contentW / 2,
                    "Paper Bill No.:", invoice.PaperBillNo);
                infoY += 18;
            }
            DrawLabelValue(gfx, x + contentW / 2, infoY, contentW / 2,
                "Date:", invoice.InvoiceDate.ToString("dd MMM yyyy"));
            infoY += 18;
            DrawLabelValue(gfx, x + contentW / 2, infoY, contentW / 2,
                "Payment:", invoice.PaymentMethod ?? "Cash");

            y = Math.Max(y + 20, infoY + 20) + 16;

            // Horizontal separator line
            gfx.DrawLine(new XPen(BorderColor, 0.5), x, y, x + contentW, y);
            y += 12;

            // ── ITEMS TABLE HEADER ────────────────────────────────────────
            double[] colW = { contentW * 0.38, contentW * 0.08, contentW * 0.13, contentW * 0.08, contentW * 0.15, contentW * 0.18 };
            string[] headers = { "Product", "Qty", "Rate (₹)", "GST%", "Batch", "Amount (₹)" };

            // Header background
            gfx.DrawRectangle(new XSolidBrush(Green), x, y, contentW, 20);
            double cx = x;
            for (int i = 0; i < headers.Length; i++)
            {
                var align = i == 0 ? XStringFormats.TopLeft : XStringFormats.TopRight;
                double padL = i == 0 ? 6 : 0;
                double padR = i == 0 ? 0 : 6;
                gfx.DrawString(headers[i], Bold(9), XBrushes.White,
                    new XRect(cx + padL, y + 4, colW[i] - padL - padR, 14), align);
                cx += colW[i];
            }
            y += 20;

            // ── ITEMS ROWS ────────────────────────────────────────────────
            bool alt = false;
            decimal rowTotal = 0;
            foreach (var item in items)
            {
                if (alt)
                    gfx.DrawRectangle(new XSolidBrush(RowAlt), x, y, contentW, 18);
                alt = !alt;

                string[] svals =
                {
                    item.ProductName ?? "",
                    item.Qty.ToString(),
                    ((double)item.Rate).ToString("N2"),
                    ((double)item.GstPercent).ToString("N1"),
                    item.BatchNo ?? "-",
                    ((double)item.Amount).ToString("N2")
                };

                cx = x;
                for (int i = 0; i < svals.Length; i++)
                {
                    var align = i == 0 ? XStringFormats.TopLeft : XStringFormats.TopRight;
                    double padL = i == 0 ? 6 : 0;
                    double padR = i == 0 ? 0 : 6;
                    gfx.DrawString(svals[i], i == 5 ? Bold(9) : Regular(9), new XSolidBrush(TextDark),
                        new XRect(cx + padL, y + 3, colW[i] - padL - padR, 14), align);
                    cx += colW[i];
                }
                rowTotal += item.Amount;
                y += 18;
            }

            // Bottom border of table
            gfx.DrawLine(new XPen(BorderColor, 0.5), x, y, x + contentW, y);
            y += 14;

            // ── TOTALS BLOCK (right-aligned) ─────────────────────────────
            double totW = contentW * 0.45;
            double totX = x + contentW - totW;

            void TotLine(string label, string val, bool bold = false, XColor? fg = null)
            {
                var fgBrush = fg.HasValue ? new XSolidBrush(fg.Value) : new XSolidBrush(TextDark);
                var fnt = bold ? Bold(10) : Regular(10);
                gfx.DrawString(label, Regular(10), new XSolidBrush(TextMuted),
                    new XRect(totX, y, totW * 0.55, 16), XStringFormats.TopLeft);
                gfx.DrawString(val, fnt, fgBrush,
                    new XRect(totX, y, totW, 16), XStringFormats.TopRight);
                y += 16;
            }

            TotLine("Subtotal", $"₹ {invoice.SubTotal:N2}");
            if (invoice.Discount > 0)
                TotLine("Discount", $"- ₹ {invoice.Discount:N2}");
            TotLine("Taxable Amount", $"₹ {invoice.TaxableAmount:N2}");
            TotLine("GST", $"₹ {invoice.GstAmount:N2}");

            y += 4;
            gfx.DrawLine(new XPen(Green, 0.8), totX, y, totX + totW, y);
            y += 8;

            TotLine("Grand Total", $"₹ {invoice.GrandTotal:N2}", bold: true, fg: Green);

            y += 4;
            gfx.DrawLine(new XPen(BorderColor, 0.5), totX, y, totX + totW, y);
            y += 12;

            // Payment summary
            TotLine("Amount Paid", $"₹ {invoice.PaidAmount:N2}");
            if (invoice.PayableAmount > 0)
                TotLine("Outstanding / Udhar", $"₹ {invoice.PayableAmount:N2}", fg: XColor.FromArgb(234, 88, 12));

            y += 12;

            // ── NOTES ──────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                gfx.DrawString("Notes:", Bold(10), new XSolidBrush(TextMuted),
                    new XRect(x, y, contentW, 14), XStringFormats.TopLeft);
                y += 14;
                gfx.DrawString(invoice.Notes, Regular(9), new XSolidBrush(TextDark),
                    new XRect(x, y, contentW, 30), XStringFormats.TopLeft);
                y += 30;
            }

            // ── TERMS ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(settings?.TermsAndConditions))
            {
                y += 8;
                gfx.DrawLine(new XPen(BorderColor, 0.5), x, y, x + contentW, y);
                y += 8;
                gfx.DrawString("Terms & Conditions:", Bold(9), new XSolidBrush(TextMuted),
                    new XRect(x, y, contentW, 14), XStringFormats.TopLeft);
                y += 14;
                gfx.DrawString(settings.TermsAndConditions, Regular(8), new XSolidBrush(TextMuted),
                    new XRect(x, y, contentW, 40), XStringFormats.TopLeft);
            }

            // ── FOOTER ────────────────────────────────────────────────────
            double footerY = page.Height.Point - 30;
            gfx.DrawLine(new XPen(BorderColor, 0.5), x, footerY, x + contentW, footerY);
            gfx.DrawString("Thank you for your business! 🙏", Regular(9), new XSolidBrush(TextMuted),
                new XRect(x, footerY + 6, contentW, 14), XStringFormats.TopCenter);
            gfx.DrawString($"Generated by KrushiBill ERP  •  {DateTime.Now:dd MMM yyyy, hh:mm tt}",
                Regular(8), new XSolidBrush(TextMuted),
                new XRect(x, footerY + 18, contentW, 12), XStringFormats.TopRight);

            doc.Save(outputPath);
            return outputPath;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static void DrawLabelValue(XGraphics gfx, double x, double y, double w,
            string label, string val)
        {
            gfx.DrawString(label, Regular(9), new XSolidBrush(TextMuted),
                new XRect(x, y, w * 0.45, 14), XStringFormats.TopLeft);
            gfx.DrawString(val, Bold(9), new XSolidBrush(TextDark),
                new XRect(x + w * 0.45, y, w * 0.55, 14), XStringFormats.TopLeft);
        }

        // Use a widely-available system font to avoid PdfSharp font resolver errors.
        private const string DefaultPdfFontFamily = "Arial";

        private static XFont Regular(double size) =>
            new XFont(DefaultPdfFontFamily, size, XFontStyleEx.Regular);

        private static XFont Bold(double size) =>
            new XFont(DefaultPdfFontFamily, size, XFontStyleEx.Bold);

        /// <summary>
        /// Generate PDF and open WhatsApp Web chat for the given phone number.
        /// This will generate the PDF, open web.whatsapp.com with a prefilled message,
        /// copy the PDF path to clipboard and open Explorer with the file selected so the user
        /// can attach the PDF quickly in WhatsApp Web.
        /// Note: Automatic attaching is not possible without browser automation. The user must
        /// complete the send action in WhatsApp Web.
        /// </summary>
        public static string GeneratePdfAndPrepareWhatsapp(int invoiceId, string phoneNumber, string outputPath = null)
        {
            // Generate PDF
            var pdfPath = GeneratePdf(invoiceId, outputPath);

            try
            {
                // Open WhatsApp Web chat with prefilled message
                var invoice = Data.DatabaseHelper.GetInvoiceById(invoiceId);
                string msg = invoice == null ? "Please find attached the invoice." : $"Please find attached Invoice {invoice.InvoiceNo} (Amount: ₹{invoice.GrandTotal}).";
                string url = $"https://web.whatsapp.com/send?phone={Uri.EscapeDataString(phoneNumber ?? "")}&text={Uri.EscapeDataString(msg)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });

                // Copy PDF path to clipboard and open Explorer with file selected
                try
                {
                    System.Windows.Clipboard.SetText(pdfPath);
                }
                catch { /* ignore clipboard failures */ }

                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{pdfPath}\"");
                }
                catch { /* ignore explorer failures */ }
            }
            catch
            {
                // Swallow errors - PDF was generated successfully so return path anyway
            }

            return pdfPath;
        }

        /// <summary>
        /// Generate PDF, open WhatsApp Web in a Chrome instance and attach the PDF file automatically.
        /// The browser uses a local Chrome profile directory so you only need to scan the QR once.
        /// This method does NOT click the final Send button; the user must press Send in WhatsApp Web.
        /// Requires Selenium.WebDriver and ChromeDriver available in the app output or PATH.
        /// </summary>
        public static string GeneratePdfAndAttachWhatsappAuto(int invoiceId, string phoneNumber, string outputPath = null)
        {
            var pdfPath = GeneratePdf(invoiceId, outputPath);

            try
            {
                var options = new ChromeOptions();
                // Persist login by using a user-data directory inside app folder (so you scan QR once)
                var profileDir = Path.Combine(AppContext.BaseDirectory, "selenium_profile");
                Directory.CreateDirectory(profileDir);
                options.AddArgument($"--user-data-dir={profileDir}");
                options.AddArgument("--start-maximized");

                // Create ChromeDriver (ensure ChromeDriver binary is available)
                var driver = new ChromeDriver(options);

                // Go to chat URL
                string msg = Uri.EscapeDataString("Please find attached the invoice.");
                string url = $"https://web.whatsapp.com/send?phone={Uri.EscapeDataString(phoneNumber ?? "")}&text={msg}";
                driver.Navigate().GoToUrl(url);

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

                // Wait for the attach (clip) button to appear - indicates chat loaded and user logged in
                wait.Until(d => d.FindElements(By.CssSelector("span[data-icon='clip']")).Count > 0);

                // Click the attach button
                var clip = driver.FindElement(By.CssSelector("span[data-icon='clip']"));
                clip.Click();

                // Wait for file input and send the file path
                wait.Until(d => d.FindElements(By.CssSelector("input[type='file']")).Count > 0);
                var fileInput = driver.FindElement(By.CssSelector("input[type='file']"));
                fileInput.SendKeys(pdfPath);

                // Wait until a send/preview element appears (attachment loaded)
                wait.Until(d => d.FindElements(By.CssSelector("span[data-icon='send'], button[data-testid='send']")).Count > 0);

                // Leave browser open so user can press Send. Return the PDF path.
                return pdfPath;
            }
            catch
            {
                // On failure, just return the generated PDF path so the user can attach manually
                return pdfPath;
            }
        }

        private static string MakeSafeFilename(string name) =>
            name == null ? "Invoice" :
            string.Concat(name.Split(Path.GetInvalidFileNameChars()));
    }

    // Simple IFontResolver implementation that looks for font files in the app fonts folder
    // and falls back to the Windows Fonts folder. Place a TTF file named "Arial.ttf" or
    // another preferred font into a "fonts" directory next to the app executable if you
    // want to bundle fonts with the app. On Windows this will normally find C:\Windows\Fonts\arial.ttf.
    internal class SimpleFontResolver : IFontResolver
    {
        private const string FaceNameKey = "Arial#";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Map requests for Arial (case-insensitive) to our embedded/system Arial face key
            if (string.Equals(familyName, "Arial", StringComparison.OrdinalIgnoreCase))
                return new FontResolverInfo(FaceNameKey);

            // Always try to satisfy with Arial as a reasonable default
            return new FontResolverInfo(FaceNameKey);
        }

        public byte[] GetFont(string faceName)
        {
            if (!string.Equals(faceName, FaceNameKey, StringComparison.Ordinal))
                return null;

            // Look in application "fonts" folder first
            string appFonts = Path.Combine(AppContext.BaseDirectory, "fonts");
            string[] candidates = new[] { "Arial.ttf", "arial.ttf", "LiberationSans-Regular.ttf", "NotoSans-Regular.ttf" };
            foreach (var c in candidates)
            {
                var p = Path.Combine(appFonts, c);
                if (File.Exists(p))
                    return File.ReadAllBytes(p);
            }

            // Fallback: Windows fonts folder
            try
            {
                string winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                foreach (var c in candidates)
                {
                    var p = Path.Combine(winFonts, c);
                    if (File.Exists(p))
                        return File.ReadAllBytes(p);
                }
            }
            catch { /* ignore access errors */ }

            // If no font found, return null and let PdfSharp continue with its own fallback (may show warning)
            return null;
        }
    }
}
