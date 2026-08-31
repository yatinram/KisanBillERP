using System;
using System.Collections;
using System.IO;
using System.Reflection;
using KrushiBillERP.Data;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace KrushiBillERP.Views
{
    public static class ReportPdfHelper
    {
        private static readonly XColor PrimaryGreen = XColor.FromArgb(27, 94, 32);   // #1B5E20
        private static readonly XColor TextDark = XColor.FromArgb(30, 36, 34);      // #1E2422
        private static readonly XColor TextMuted = XColor.FromArgb(108, 117, 125);  // #6C757D
        private static readonly XColor BorderColor = XColor.FromArgb(226, 232, 240); // #E2E8F0
        private static readonly XColor LightBg = XColor.FromArgb(248, 250, 249);     // #F8FAF9

        public static string GenerateReportPdf(string reportTitle, string summaryText, IEnumerable dataItems, string outputPath = null)
        {
            if (GlobalFontSettings.FontResolver == null)
                GlobalFontSettings.FontResolver = new SimpleFontResolver();

            var settings = DatabaseHelper.GetCompanySettings();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string tempDir = Path.GetTempPath();
                outputPath = Path.Combine(tempDir, $"{reportTitle.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            }

            using var doc = new PdfDocument();
            doc.Info.Title = reportTitle;

            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(210); // A4
            page.Height = XUnit.FromMillimeter(297);

            var gfx = XGraphics.FromPdfPage(page);

            double margin = 36;
            double pageW = page.Width.Point;
            double x = margin;
            double y = margin;
            double contentW = pageW - margin * 2;

            // ── HEADER BAND ──────────────────────────────────────────────
            gfx.DrawRectangle(new XSolidBrush(PrimaryGreen), x - margin, 0, pageW, 72);

            var shopName = settings?.ShopName ?? "KRUSHI KENDRA AGRICULTURE & PESTICIDES";
            // Shop name: LEFT half only — never overlaps with right-side report title
            gfx.DrawString(shopName, Bold(13), XBrushes.White,
                new XRect(x, 10, contentW * 0.55, 20), XStringFormats.TopLeft);

            if (!string.IsNullOrWhiteSpace(settings?.ShopAddress))
            {
                gfx.DrawString(settings.ShopAddress, Regular(8), XBrushes.White,
                    new XRect(x, 32, contentW * 0.55, 12), XStringFormats.TopLeft);
            }

            // Report title: RIGHT half, anchored right — never overlaps shop name
            gfx.DrawString(reportTitle.ToUpper(), Bold(12), XBrushes.White,
                new XRect(x + contentW * 0.45, 10, contentW * 0.55, 20), XStringFormats.TopRight);

            gfx.DrawString($"Generated: {DateTime.Now:dd MMM yyyy, hh:mm tt}", Regular(8), XBrushes.White,
                new XRect(x + contentW * 0.45, 32, contentW * 0.55, 12), XStringFormats.TopRight);

            y = 84;

            // ── SUMMARY BANNER ───────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(summaryText))
            {
                gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, 30);
                gfx.DrawRectangle(new XPen(BorderColor, 1), x, y, contentW, 30);
                gfx.DrawString(summaryText, Bold(9), new XSolidBrush(PrimaryGreen),
                    new XRect(x + 10, y + 8, contentW - 20, 16), XStringFormats.TopLeft);
                y += 40;
            }

            // ── DATA TABLE ──────────────────────────────────────────────
            if (dataItems != null)
            {
                PropertyInfo[] props = null;
                var list = new System.Collections.Generic.List<object>();
                foreach (var item in dataItems)
                {
                    if (props == null) props = item.GetType().GetProperties();
                    list.Add(item);
                }

                if (props != null && props.Length > 0 && list.Count > 0)
                {
                    int colCount = props.Length;
                    double colWidth = contentW / colCount;

                    const double hdrH = 24;
                    const double rH   = 20;

                    void DrawTableHeader(XGraphics g, double startY)
                    {
                        g.DrawRectangle(new XSolidBrush(PrimaryGreen), x, startY, contentW, hdrH);
                        for (int i = 0; i < colCount; i++)
                        {
                            string headerName = FormatHeaderName(props[i].Name);
                            g.DrawString(headerName, Bold(8.5), XBrushes.White,
                                new XRect(x + (i * colWidth) + 5, startY, colWidth - 10, hdrH), XStringFormats.CenterLeft);
                            if (i < colCount - 1)
                                g.DrawLine(new XPen(XColors.White, 0.4),
                                    x + ((i + 1) * colWidth), startY,
                                    x + ((i + 1) * colWidth), startY + hdrH);
                        }
                    }

                    // Table Header
                    DrawTableHeader(gfx, y);
                    y += hdrH;

                    // Rows
                    int rIdx = 1;
                    foreach (var rowItem in list)
                    {
                        if (y > page.Height.Point - 60)
                        {
                            // Page break — add new page, re-draw header
                            page = doc.AddPage();
                            page.Width = XUnit.FromMillimeter(210);
                            page.Height = XUnit.FromMillimeter(297);
                            gfx.Dispose();
                            gfx = XGraphics.FromPdfPage(page);
                            DrawTableHeader(gfx, margin);
                            y = margin + hdrH;
                        }

                        bool isAlt = (rIdx % 2 == 0);
                        if (isAlt)
                            gfx.DrawRectangle(new XSolidBrush(LightBg), x, y, contentW, rH);

                        for (int i = 0; i < colCount; i++)
                        {
                            string cellVal = props[i].GetValue(rowItem)?.ToString() ?? "";
                            gfx.DrawString(cellVal, Regular(8), new XSolidBrush(TextDark),
                                new XRect(x + (i * colWidth) + 5, y, colWidth - 10, rH), XStringFormats.CenterLeft);
                            if (i < colCount - 1)
                                gfx.DrawLine(new XPen(BorderColor, 0.3),
                                    x + ((i + 1) * colWidth), y,
                                    x + ((i + 1) * colWidth), y + rH);
                        }

                        gfx.DrawLine(new XPen(BorderColor, 0.3), x, y + rH, x + contentW, y + rH);
                        y += rH;
                        rIdx++;
                    }

                    gfx.DrawLine(new XPen(BorderColor, 1), x, y, x + contentW, y);
                }
            }

            // Footer
            gfx.DrawString("KrushiBill ERP - Commercial Billing & Inventory Management System", Regular(8), new XSolidBrush(TextMuted),
                new XRect(x, page.Height.Point - 30, contentW, 14), XStringFormats.TopCenter);

            doc.Save(outputPath);
            return outputPath;
        }

        private static string FormatHeaderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append(" ");
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        private static XFont Regular(double size) => new XFont("Arial", size, XFontStyleEx.Regular);
        private static XFont Bold(double size) => new XFont("Arial", size, XFontStyleEx.Bold);
    }
}
