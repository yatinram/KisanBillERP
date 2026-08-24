using System;

namespace KrushiBillERP.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string ShopName { get; set; }
        public string ShopAddress { get; set; }
        public string ShopPhone { get; set; }
        public string Role { get; set; }
    }

    public static class AppSession
    {
        public static User CurrentUser { get; set; }

        public static bool CanEditOrDelete =>
            CurrentUser != null &&
            (string.Equals(CurrentUser.Username, "yatin", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
    }

    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string GSTIN { get; set; }
    }

    public class Purchase
    {
        public int PurchaseId { get; set; }
        public string PurchaseNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string SupplierInvoiceNumber { get; set; }
        public string PaperBillNumber { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal RoundOff { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PayableAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentReference { get; set; }
        public DateTime CreatedAt { get; set; }

        public string PaymentStatus
        {
            get
            {
                if (PaidAmount >= GrandTotal || PayableAmount <= 0) return "Paid";
                if (PaidAmount > 0) return "Partial";
                return "Unpaid";
            }
        }
    }

    public class PurchaseItem
    {
        public int PurchaseItemId { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Company { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int Quantity { get; set; }
        public int FreeQuantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal GST { get; set; }
        public decimal Amount { get; set; }
        // Additional fields to help product matching/creation
        public decimal PackSize { get; set; }
        public string Unit { get; set; }
        public decimal SellingPrice { get; set; }
        public string HSN { get; set; }
        public string CategoryName { get; set; }
    }

    public class PurchaseReturn
    {
        public int PurchaseReturnId { get; set; }
        public string ReturnNumber { get; set; }
        public int PurchaseId { get; set; }
        public string PurchaseNumber { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string SupplierInvoiceNumber { get; set; }
        public string PaperBillNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal RoundOff { get; set; }
        public decimal GrandTotal { get; set; }
        public string ReturnReason { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; } = "Completed";
        public DateTime CreatedAt { get; set; }
    }

    public class PurchaseReturnItem
    {
        public int PurchaseReturnItemId { get; set; }
        public int PurchaseReturnId { get; set; }
        public int PurchaseItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Company { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int PurchasedQuantity { get; set; }
        public int AlreadyReturnedQuantity { get; set; }
        public int ReturnableQuantity { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal GST { get; set; }
        public decimal Amount { get; set; }
    }

    public class SalesReturn
    {
        public int SalesReturnId { get; set; }
        public string ReturnNumber { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string MobileNumber { get; set; }
        public string VillageName { get; set; }
        public DateTime ReturnDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal RoundOff { get; set; }
        public decimal GrandTotal { get; set; }
        public string AdjustmentType { get; set; } = "Udhar Adjustment"; // "Udhar Adjustment", "Cash Refund"
        public string ReturnReason { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; } = "Completed";
        public DateTime CreatedAt { get; set; }
    }

    public class SalesReturnItem
    {
        public int SalesReturnItemId { get; set; }
        public int SalesReturnId { get; set; }
        public int InvoiceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Company { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int PurchasedQuantity { get; set; }
        public int AlreadyReturnedQuantity { get; set; }
        public int ReturnableQuantity { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal Rate { get; set; }
        public decimal GstPercent { get; set; }
        public decimal Amount { get; set; }
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        // New: unique SKU / product code
        public string ProductCode { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Company { get; set; }
        // Pack size (e.g., 100, 250, 1.5)
        public decimal PackSize { get; set; }
        public string BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Unit { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal GstPercent { get; set; }
        public int StockQty { get; set; }
        // Initial/last purchase quantity recorded on product master (do not treat edit as a stock transaction)
        public int PurchaseStockQty { get; set; }
        public int ReorderLevel { get; set; }
        // 1 = Active, 0 = Inactive
        public int Status { get; set; } = 1;
        // Optional HSN / SAC code
        public string HSN { get; set; }

        // Computed / UI helper properties (not persisted)
        public string StatusText => Status == 1 ? "Active" : "Inactive";

        public string StockStatus
        {
            get
            {
                if (StockQty <= 0) return "Out of Stock";
                if (StockQty <= ReorderLevel) return "Low Stock";
                return "In Stock";
            }
        }

        public bool IsExpiringSoon
        {
            get
            {
                if (!ExpiryDate.HasValue) return false;
                var today = DateTime.Today;
                var diff = (ExpiryDate.Value.Date - today).TotalDays;
                return diff >= 0 && diff <= 15;
            }
        }

        public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd MMM yyyy") : "-";

        /// <summary>
        /// UI display name for billing ComboBox showing stock info. Not persisted.
        /// </summary>
        public string DisplayName { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string GSTIN { get; set; }
    }

    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; }
        public string PaperBillNo { get; set; }
        public int CustomerId { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string CustomerName { get; set; }
        public string MobileNumber { get; set; }
        public string VillageName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GstAmount { get; set; }
        public decimal RoundOff { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // "Cash", "UPI", "Udhar"
        public decimal PaidAmount { get; set; }
        public decimal PayableAmount { get; set; } // Outstanding amount for this bill
        public string PaymentReference { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; } = "Active"; // "Active", "Cancelled"
        public int RowNo { get; set; }
    }

    public class InvoiceItem
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Company { get; set; }
        public string BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Unit { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal GstPercent { get; set; }
        public decimal Amount { get; set; }
        public string HSN { get; set; }

        public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd MMM yyyy") : "-";
    }

    public class Farmer
    {
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string MobileNumber { get; set; }
        public string VillageName { get; set; }
        // 1 = Active, 0 = Inactive
        public int Status { get; set; } = 1;
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        // Helper
        public string StatusText => Status == 1 ? "Active" : "Inactive";
    }

    public class PaymentReceipt
    {
        public int PaymentReceiptId { get; set; }
        public string ReceiptNumber { get; set; }
        public int FarmerId { get; set; }
        public string FarmerName { get; set; }
        public string MobileNumber { get; set; }
        public string VillageName { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ClosingBalance { get; set; }
        public string PaymentMode { get; set; }
        public string TransactionReference { get; set; }
        public string ChequeNumber { get; set; }
        public string ChequeDate { get; set; }
        public string BankName { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentReceiptAllocation
    {
        public int PaymentReceiptAllocationId { get; set; }
        public int PaymentReceiptId { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal InvoiceOutstanding { get; set; }
        public decimal AllocatedAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Converts decimal rupee amounts to Indian-English words.
    /// Example: 10500.50 → "Ten Thousand Five Hundred Rupees and Fifty Paise Only"
    /// </summary>
    public static class AmountToWordsHelper
    {
        private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        public static string Convert(decimal amount)
        {
            if (amount == 0) return "Zero Rupees Only";

            long rupees = (long)Math.Floor(Math.Abs(amount));
            int paise = (int)Math.Round((Math.Abs(amount) - rupees) * 100);

            string result = "";
            if (rupees > 0)
            {
                result = ConvertNumber(rupees) + " Rupees";
            }
            if (paise > 0)
            {
                result += (rupees > 0 ? " and " : "") + ConvertNumber(paise) + " Paise";
            }
            result += " Only";
            if (amount < 0) result = "Minus " + result;
            return result.Trim();
        }

        private static string ConvertNumber(long number)
        {
            if (number == 0) return "";
            if (number < 0) return "Minus " + ConvertNumber(-number);

            string words = "";

            if (number / 10000000 > 0)
            {
                words += ConvertNumber(number / 10000000) + " Crore ";
                number %= 10000000;
            }
            if (number / 100000 > 0)
            {
                words += ConvertNumber(number / 100000) + " Lakh ";
                number %= 100000;
            }
            if (number / 1000 > 0)
            {
                words += ConvertNumber(number / 1000) + " Thousand ";
                number %= 1000;
            }
            if (number / 100 > 0)
            {
                words += ConvertNumber(number / 100) + " Hundred ";
                number %= 100;
            }
            if (number > 0)
            {
                if (words != "") words += "and ";
                if (number < 20)
                    words += Ones[number];
                else
                {
                    words += Tens[number / 10];
                    if (number % 10 > 0)
                        words += " " + Ones[number % 10];
                }
            }
            return words.Trim();
        }
    }

    public class CompanySettings
    {
        public int Id { get; set; }
        public string ShopName { get; set; } = "Krushi Kendra Agriculture";
        public string ShopAddress { get; set; } = "Main Market Road, District";
        public string ShopPhone { get; set; } = "+91 98765 43210";
        public string GSTIN { get; set; } = "";
        public string LicenseNumber { get; set; } = "";
        public string BankName { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string IFSCCode { get; set; } = "";
        public string UpiId { get; set; } = "";
        public string TermsAndConditions { get; set; } = "1. Goods once sold will not be taken back.\n2. Subject to local jurisdiction.";
        public string FooterMessage { get; set; } = "Thank you for doing business with us!";
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class StockAdjustment
    {
        public int AdjustmentId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string BatchNumber { get; set; }
        public int PreviousQty { get; set; }
        public int NewQty { get; set; }
        public int DeltaQty { get; set; }
        public string AdjustmentType { get; set; } // "ADD", "REDUCE", "SET"
        public string Reason { get; set; }
        public string Notes { get; set; }
        public string AdjustedBy { get; set; } = "Admin";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents one sale transaction record for a specific product.
    /// </summary>
    public class ProductSaleRecord
    {
        public int InvoiceId { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal GstPercent { get; set; }
        public decimal Amount { get; set; }
        public string InvoiceDateDisplay => InvoiceDate.ToString("dd MMM yyyy");
    }

    /// <summary>
    /// Represents one purchase transaction record for a specific product.
    /// </summary>
    public class ProductPurchaseRecord
    {
        public int PurchaseId { get; set; }
        public string PurchaseNumber { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string SupplierName { get; set; }
        public int Quantity { get; set; }
        public int FreeQuantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal Amount { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string TransactionType { get; set; } = "Purchase Entry"; // "Purchase Entry" or "Purchase Return"
        public int TotalQty => Quantity + FreeQuantity;
        public string PurchaseDateDisplay => PurchaseDate.ToString("dd MMM yyyy");
        public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("dd MMM yyyy") : "-";
    }
}
