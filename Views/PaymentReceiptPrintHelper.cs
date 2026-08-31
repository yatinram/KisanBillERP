using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public static class PaymentReceiptPrintHelper
    {
        public static void PrintReceipt(int paymentReceiptId)
        {
            var receipt = DatabaseHelper.GetPaymentReceiptById(paymentReceiptId);
            if (receipt == null)
            {
                MessageBox.Show("Payment receipt record not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var allocations = DatabaseHelper.GetPaymentReceiptAllocations(paymentReceiptId);

            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                var doc = CreateFlowDocument(receipt, allocations);
                doc.PageWidth = printDlg.PrintableAreaWidth;
                doc.PageHeight = printDlg.PrintableAreaHeight;
                doc.PagePadding = new Thickness(40);
                doc.ColumnWidth = printDlg.PrintableAreaWidth;

                IDocumentPaginatorSource idpSource = doc;
                printDlg.PrintDocument(idpSource.DocumentPaginator, $"Payment Receipt - {receipt.ReceiptNumber}");
            }
        }

        // Expose FlowDocument for preview purposes
        public static FlowDocument GetReceiptFlowDocument(int paymentReceiptId)
        {
            var receipt = DatabaseHelper.GetPaymentReceiptById(paymentReceiptId);
            if (receipt == null) return null;
            var allocations = DatabaseHelper.GetPaymentReceiptAllocations(paymentReceiptId);
            return CreateFlowDocument(receipt, allocations);
        }

        private static FlowDocument CreateFlowDocument(PaymentReceipt receipt, List<PaymentReceiptAllocation> allocations)
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

            var pTitle = new Paragraph(new Run("PAYMENT RECEIPT / JAMA PAVTI"))
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
            r1.Cells.Add(new TableCell(new Paragraph(new Run($"Receipt No: {receipt.ReceiptNumber}")) { FontWeight = FontWeights.Bold }));
            r1.Cells.Add(new TableCell(new Paragraph(new Run($"Receipt Date: {receipt.ReceiptDate:dd MMM yyyy}"))));
            metaGroup.Rows.Add(r1);

            var r2 = new TableRow();
            r2.Cells.Add(new TableCell(new Paragraph(new Run($"Customer / Farmer: {receipt.FarmerName}")) { FontWeight = FontWeights.Bold }));
            r2.Cells.Add(new TableCell(new Paragraph(new Run($"Mobile: {receipt.MobileNumber}"))));
            metaGroup.Rows.Add(r2);

            var r3 = new TableRow();
            r3.Cells.Add(new TableCell(new Paragraph(new Run($"Village: {receipt.VillageName}"))));
            r3.Cells.Add(new TableCell(new Paragraph(new Run($"Payment Mode: {receipt.PaymentMode}")) { FontWeight = FontWeights.SemiBold }));
            metaGroup.Rows.Add(r3);

            if (!string.IsNullOrWhiteSpace(receipt.TransactionReference))
            {
                var r4 = new TableRow();
                r4.Cells.Add(new TableCell(new Paragraph(new Run($"Txn Ref: {receipt.TransactionReference}"))));
                r4.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                metaGroup.Rows.Add(r4);
            }

            if (!string.IsNullOrWhiteSpace(receipt.ChequeNumber))
            {
                var r5 = new TableRow();
                r5.Cells.Add(new TableCell(new Paragraph(new Run($"Cheque No: {receipt.ChequeNumber} ({receipt.ChequeDate})"))));
                r5.Cells.Add(new TableCell(new Paragraph(new Run($"Bank: {receipt.BankName}"))));
                metaGroup.Rows.Add(r5);
            }

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            // Spacer
            doc.Blocks.Add(new Paragraph(new Run("")) { Margin = new Thickness(0, 10, 0, 10) });

            // Balance Summary Card Table
            var balTable = new Table { CellSpacing = 0, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAF9")), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1) };
            balTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            balTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            balTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var balGroup = new TableRowGroup();
            var balRow = new TableRow();
            balRow.Cells.Add(new TableCell(new Paragraph(new Run($"Opening Balance\n₹ {receipt.OpeningBalance:N2}")) { TextAlignment = TextAlignment.Center }));
            balRow.Cells.Add(new TableCell(new Paragraph(new Run($"Received Amount\n₹ {receipt.ReceivedAmount:N2}")) { TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Bold, Foreground = Brushes.Green }));
            balRow.Cells.Add(new TableCell(new Paragraph(new Run($"Closing Balance\n₹ {receipt.ClosingBalance:N2}")) { TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Bold }));
            balGroup.Rows.Add(balRow);
            balTable.RowGroups.Add(balGroup);
            doc.Blocks.Add(balTable);

            // Amount in words
            string words = AmountToWordsHelper.Convert(receipt.ReceivedAmount);
            var pWords = new Paragraph(new Run($"Amount in Words: {words}")) { Margin = new Thickness(0, 10, 0, 14), FontWeight = FontWeights.SemiBold, FontStyle = FontStyles.Italic };
            doc.Blocks.Add(pWords);

            // Allocations Table
            if (allocations != null && allocations.Count > 0)
            {
                var allocTitle = new Paragraph(new Run("PAYMENT ALLOCATION DETAILS")) { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4) };
                doc.Blocks.Add(allocTitle);

                var allocTable = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 1) };
                allocTable.Columns.Add(new TableColumn { Width = new GridLength(40) });
                allocTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                allocTable.Columns.Add(new TableColumn { Width = new GridLength(130) });
                allocTable.Columns.Add(new TableColumn { Width = new GridLength(130) });

                var allocGroup = new TableRowGroup();

                var hRow = new TableRow { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAF9")) };
                hRow.Cells.Add(new TableCell(new Paragraph(new Run("#")) { FontWeight = FontWeights.Bold }));
                hRow.Cells.Add(new TableCell(new Paragraph(new Run("Invoice No.")) { FontWeight = FontWeights.Bold }));
                hRow.Cells.Add(new TableCell(new Paragraph(new Run("Invoice Date")) { FontWeight = FontWeights.Bold }));
                hRow.Cells.Add(new TableCell(new Paragraph(new Run("Allocated Amount")) { FontWeight = FontWeights.Bold }));
                allocGroup.Rows.Add(hRow);

                int idx = 1;
                decimal totalAlloc = 0m;
                foreach (var a in allocations)
                {
                    var row = new TableRow();
                    row.Cells.Add(new TableCell(new Paragraph(new Run(idx++.ToString()))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(a.InvoiceNo))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(a.InvoiceDate.ToString("dd MMM yyyy")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {a.AllocatedAmount:N2}")) { FontWeight = FontWeights.SemiBold }));
                    allocGroup.Rows.Add(row);
                    totalAlloc += a.AllocatedAmount;
                }

                // Total row
                var totRow = new TableRow { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")) };
                totRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                totRow.Cells.Add(new TableCell(new Paragraph(new Run("Total Allocated")) { FontWeight = FontWeights.Bold }));
                totRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                totRow.Cells.Add(new TableCell(new Paragraph(new Run($"₹ {totalAlloc:N2}")) { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkGreen }));
                allocGroup.Rows.Add(totRow);

                allocTable.RowGroups.Add(allocGroup);
                doc.Blocks.Add(allocTable);
            }

            if (!string.IsNullOrWhiteSpace(receipt.Notes))
            {
                var pNotes = new Paragraph(new Run($"Notes: {receipt.Notes}")) { Margin = new Thickness(0, 10, 0, 0), Foreground = Brushes.Gray };
                doc.Blocks.Add(pNotes);
            }

            // Signatures
            var pSign = new Paragraph(new Run("\n\n_______________________\nAuthorised Signatory")) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 30, 0, 0) };
            doc.Blocks.Add(pSign);

            return doc;
        }
    }
}
