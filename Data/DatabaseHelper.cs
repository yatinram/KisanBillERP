using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using KrushiBillERP.Models;

namespace KrushiBillERP.Data
{
    /// <summary>
    /// Handles all SQLite access for the application. Everything is table/column driven
    /// (no hard-coded shop data) so the same EXE + a fresh krushibill.db works for any
    /// seeds & pesticides shop that installs it.
    /// </summary>
    public static class DatabaseHelper
    {
        // DB file is created next to the EXE - each shop's installation gets its own data.
        private static readonly string DbFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "krushibill.db");

        private static string ConnectionString => $"Data Source={DbFile}";

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        // ---------------- Farmers ----------------

        public static int GetTotalFarmers()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Farmers";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static int GetActiveFarmersCount()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Farmers WHERE Status=1";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static (List<Models.Farmer> Items, int Total) GetFarmersPaged(string search = null, int status = -1, int page = 1, int pageSize = 20)
        {
            var list = new List<Models.Farmer>();
            using var conn = GetConnection();
            var whereClauses = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) whereClauses.Add("(FarmerName LIKE $s OR MobileNumber LIKE $s OR VillageName LIKE $s)");
            if (status == 0 || status == 1) whereClauses.Add("Status = $st");
            var where = whereClauses.Count == 0 ? "1=1" : string.Join(" AND ", whereClauses);

            var cntCmd = conn.CreateCommand();
            cntCmd.CommandText = $"SELECT COUNT(*) FROM Farmers WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cntCmd.Parameters.AddWithValue("$s", $"%{search}%");
            if (status == 0 || status == 1) cntCmd.Parameters.AddWithValue("$st", status);
            var total = Convert.ToInt32((long)cntCmd.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = $"SELECT * FROM Farmers WHERE {where} ORDER BY FarmerName LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) dataCmd.Parameters.AddWithValue("$s", $"%{search}%");
            if (status == 0 || status == 1) dataCmd.Parameters.AddWithValue("$st", status);
            dataCmd.Parameters.AddWithValue("$limit", pageSize);
            dataCmd.Parameters.AddWithValue("$offset", offset);
            using var r = dataCmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Models.Farmer
                {
                    FarmerId = r.GetInt32(r.GetOrdinal("FarmerId")),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"]),
                    CreatedDate = string.IsNullOrWhiteSpace(r["CreatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedDate"].ToString()),
                    UpdatedDate = string.IsNullOrWhiteSpace(r["UpdatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["UpdatedDate"].ToString())
                });
            }
            return (list, total);
        }

        public static List<Models.Farmer> GetAllFarmers()
        {
            var list = new List<Models.Farmer>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Farmers ORDER BY FarmerName";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Models.Farmer
                {
                    FarmerId = r.GetInt32(r.GetOrdinal("FarmerId")),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"]),
                    CreatedDate = string.IsNullOrWhiteSpace(r["CreatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedDate"].ToString()),
                    UpdatedDate = string.IsNullOrWhiteSpace(r["UpdatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["UpdatedDate"].ToString())
                });
            }
            return list;
        }

        public static Models.Farmer GetFarmerById(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Farmers WHERE FarmerId=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new Models.Farmer
                {
                    FarmerId = r.GetInt32(r.GetOrdinal("FarmerId")),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"]),
                    CreatedDate = string.IsNullOrWhiteSpace(r["CreatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedDate"].ToString()),
                    UpdatedDate = string.IsNullOrWhiteSpace(r["UpdatedDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["UpdatedDate"].ToString())
                };
            }
            return null;
        }

        public static bool IsFarmerMobileExists(string mobile, int excludeFarmerId = 0)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return false;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Farmers WHERE MobileNumber=$m" + (excludeFarmerId > 0 ? " AND FarmerId<>$id" : "");
            cmd.Parameters.AddWithValue("$m", mobile);
            if (excludeFarmerId > 0) cmd.Parameters.AddWithValue("$id", excludeFarmerId);
            var cnt = Convert.ToInt32((long)cmd.ExecuteScalar());
            return cnt > 0;
        }

        public static void SaveFarmer(Models.Farmer f)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            if (f.FarmerId == 0)
            {
                cmd.CommandText = "INSERT INTO Farmers (FarmerName, MobileNumber, VillageName, Status, CreatedDate, UpdatedDate) VALUES ($n,$m,$v,$st,$cd,$ud)";
                cmd.Parameters.AddWithValue("$cd", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                cmd.CommandText = "UPDATE Farmers SET FarmerName=$n, MobileNumber=$m, VillageName=$v, Status=$st, UpdatedDate=$ud WHERE FarmerId=$id";
                cmd.Parameters.AddWithValue("$id", f.FarmerId);
            }
            cmd.Parameters.AddWithValue("$n", f.FarmerName ?? "");
            cmd.Parameters.AddWithValue("$m", f.MobileNumber ?? "");
            cmd.Parameters.AddWithValue("$v", f.VillageName ?? "");
            cmd.Parameters.AddWithValue("$st", f.Status);
            cmd.Parameters.AddWithValue("$ud", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public static void DeleteOrDeactivateFarmer(int id)
        {
            using var conn = GetConnection();
            // Check if any existing tables reference FarmerId (Invoice or other) - if so mark inactive
            var colCmd = conn.CreateCommand();
            colCmd.CommandText = "PRAGMA table_info(Invoices);";
            bool hasFarmerCol = false;
            using (var r = colCmd.ExecuteReader())
            {
                while (r.Read()) if (r[1]?.ToString() == "FarmerId") { hasFarmerCol = true; break; }
            }
            if (hasFarmerCol)
            {
                var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Invoices WHERE FarmerId=$id";
                checkCmd.Parameters.AddWithValue("$id", id);
                var cnt = Convert.ToInt32((long)checkCmd.ExecuteScalar());
                if (cnt > 0)
                {
                    var upd = conn.CreateCommand();
                    upd.CommandText = "UPDATE Farmers SET Status=0 WHERE FarmerId=$id";
                    upd.Parameters.AddWithValue("$id", id);
                    upd.ExecuteNonQuery();
                    return;
                }
            }
            // No historical references - safe to delete
            var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM Farmers WHERE FarmerId=$id";
            del.Parameters.AddWithValue("$id", id);
            del.ExecuteNonQuery();
        }

        // ---------------- Dashboard helpers ----------------

        public static (decimal Cash, decimal Online, decimal Udhar, decimal Total) GetRevenueBreakdown(DateTime start, DateTime end)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT IFNULL(SUM(CASE WHEN PaymentMethod NOT LIKE '%UPI%' AND PaymentMethod NOT LIKE '%Online%' THEN PaidAmount ELSE 0 END),0) as Cash,
                       IFNULL(SUM(CASE WHEN PaymentMethod LIKE '%UPI%' OR PaymentMethod LIKE '%Online%' THEN PaidAmount ELSE 0 END),0) as Online,
                       IFNULL(SUM(PayableAmount),0) as Udhar,
                       IFNULL(SUM(GrandTotal),0) as Total
                FROM Invoices
                WHERE datetime(InvoiceDate) >= $s AND datetime(InvoiceDate) <= $e AND (Status IS NULL OR Status = 'Active')";
            cmd.Parameters.AddWithValue("$s", start.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$e", end.ToString("yyyy-MM-dd HH:mm:ss"));
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                var cash = r.IsDBNull(0) ? 0 : Convert.ToDecimal(r.GetDouble(0));
                var online = r.IsDBNull(1) ? 0 : Convert.ToDecimal(r.GetDouble(1));
                var udhar = r.IsDBNull(2) ? 0 : Convert.ToDecimal(r.GetDouble(2));
                var total = r.IsDBNull(3) ? 0 : Convert.ToDecimal(r.GetDouble(3));
                return (cash, online, udhar, total);
            }
            return (0, 0, 0, 0);
        }

        public static List<(string Label, decimal Value)> GetSalesSeries(DateTime start, DateTime end, string period)
        {
            var list = new List<(string, decimal)>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            if (period == "today")
            {
                // group by hour
                cmd.CommandText = @"SELECT strftime('%H', InvoiceDate) as H, IFNULL(SUM(GrandTotal),0) as S FROM Invoices
                    WHERE datetime(InvoiceDate) >= $s AND datetime(InvoiceDate) <= $e
                    GROUP BY H ORDER BY H";
            }
            else if (period == "week")
            {
                // group by weekday (0=Sun..6=Sat)
                cmd.CommandText = @"SELECT strftime('%w', InvoiceDate) as W, IFNULL(SUM(GrandTotal),0) as S FROM Invoices
                    WHERE date(InvoiceDate) >= date($s) AND date(InvoiceDate) <= date($e)
                    GROUP BY W ORDER BY W";
            }
            else if (period == "month")
            {
                cmd.CommandText = @"SELECT strftime('%d', InvoiceDate) as D, IFNULL(SUM(GrandTotal),0) as S FROM Invoices
                    WHERE date(InvoiceDate) >= date($s) AND date(InvoiceDate) <= date($e)
                    GROUP BY D ORDER BY D";
            }
            else // year
            {
                cmd.CommandText = @"SELECT strftime('%m', InvoiceDate) as M, IFNULL(SUM(GrandTotal),0) as S FROM Invoices
                    WHERE date(InvoiceDate) >= date($s) AND date(InvoiceDate) <= date($e)
                    GROUP BY M ORDER BY M";
            }
            cmd.Parameters.AddWithValue("$s", start.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$e", end.ToString("yyyy-MM-dd HH:mm:ss"));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var label = r.GetString(0);
                var val = r.IsDBNull(1) ? 0 : Convert.ToDecimal(r.GetDouble(1));
                list.Add((label, val));
            }
            return list;
        }

        // ---------------- Suppliers ----------------
        public static List<Supplier> GetSuppliers(string search = null)
        {
            var list = new List<Supplier>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Phone, Address, GSTIN FROM Suppliers WHERE ($s IS NULL OR Name LIKE $s OR Phone LIKE $s) ORDER BY Name";
            cmd.Parameters.AddWithValue("$s", string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : $"%{search}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Supplier
                {
                    Id = r.GetInt32(0),
                    Name = r[1]?.ToString(),
                    Phone = r[2]?.ToString(),
                    Address = r[3]?.ToString(),
                    GSTIN = r[4]?.ToString()
                });
            }
            return list;
        }

        public static int SaveSupplier(Supplier s)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            if (s.Id == 0)
            {
                cmd.CommandText = "INSERT INTO Suppliers (Name, Phone, Address, GSTIN) VALUES ($n,$p,$a,$g); SELECT last_insert_rowid();";
            }
            else
            {
                cmd.CommandText = "UPDATE Suppliers SET Name=$n, Phone=$p, Address=$a, GSTIN=$g WHERE Id=$id; SELECT $id;";
                cmd.Parameters.AddWithValue("$id", s.Id);
            }
            cmd.Parameters.AddWithValue("$n", s.Name ?? "");
            cmd.Parameters.AddWithValue("$p", s.Phone ?? "");
            cmd.Parameters.AddWithValue("$a", s.Address ?? "");
            cmd.Parameters.AddWithValue("$g", s.GSTIN ?? "");
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        // ---------------- Purchases ----------------
        public static string GenerateNextPurchaseNo()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(PurchaseId), 0) FROM Purchases";
            long maxId = Convert.ToInt64(cmd.ExecuteScalar());
            return $"PUR-{DateTime.Now:yyyy}-{(maxId + 1):00000}";
        }

        public static int SavePurchase(Purchase purchase, List<PurchaseItem> items)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Ensure supplier exists by Name (matching case-insensitive)
                if (purchase.SupplierId == 0 && !string.IsNullOrWhiteSpace(purchase.SupplierName))
                {
                    string sName = purchase.SupplierName.Trim();
                    var chkSup = conn.CreateCommand();
                    chkSup.CommandText = "SELECT Id FROM Suppliers WHERE LOWER(Name) = LOWER($n) LIMIT 1";
                    chkSup.Parameters.AddWithValue("$n", sName);
                    var existingSupId = chkSup.ExecuteScalar();
                    if (existingSupId != null && existingSupId != DBNull.Value)
                    {
                        purchase.SupplierId = Convert.ToInt32((long)existingSupId);
                    }
                    else
                    {
                        var scmd = conn.CreateCommand();
                        scmd.CommandText = "INSERT INTO Suppliers (Name) VALUES ($n); SELECT last_insert_rowid();";
                        scmd.Parameters.AddWithValue("$n", sName);
                        purchase.SupplierId = Convert.ToInt32((long)scmd.ExecuteScalar());
                    }
                }

                // 2. Insert Purchase Header
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Purchases (PurchaseNumber, SupplierId, SupplierInvoiceNumber, PaperBillNumber, PurchaseDate, SubTotal, Discount, TaxableAmount, GSTAmount, RoundOff, GrandTotal, PaidAmount, PayableAmount, PaymentMethod, PaymentReference, CreatedAt)
                    VALUES ($no,$sid,$sino,$pno,$dt,$sub,$disc,$tax,$gst,$ro,$grand,$paid,$payable,$pm,$pref,$ca);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$no", purchase.PurchaseNumber);
                cmd.Parameters.AddWithValue("$sid", purchase.SupplierId);
                cmd.Parameters.AddWithValue("$sino", purchase.SupplierInvoiceNumber?.Trim() ?? "");
                cmd.Parameters.AddWithValue("$pno", purchase.PaperBillNumber?.Trim() ?? "");
                cmd.Parameters.AddWithValue("$dt", purchase.PurchaseDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$sub", purchase.SubTotal);
                cmd.Parameters.AddWithValue("$disc", purchase.Discount);
                cmd.Parameters.AddWithValue("$tax", purchase.TaxableAmount);
                cmd.Parameters.AddWithValue("$gst", purchase.GSTAmount);
                cmd.Parameters.AddWithValue("$ro", purchase.RoundOff);
                cmd.Parameters.AddWithValue("$grand", purchase.GrandTotal);
                cmd.Parameters.AddWithValue("$paid", purchase.PaidAmount);
                cmd.Parameters.AddWithValue("$payable", purchase.PayableAmount);
                cmd.Parameters.AddWithValue("$pm", purchase.PaymentMethod ?? "Cash");
                cmd.Parameters.AddWithValue("$pref", purchase.PaymentReference?.Trim() ?? "");
                cmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                int purchaseId = Convert.ToInt32((long)cmd.ExecuteScalar());

                // 3. Process each purchase item
                foreach (var item in items)
                {
                    int productId = item.ProductId;
                    if (productId == 0)
                    {
                        // try to find if product ALREADY exists in Products catalog by Name and Company
                        var find = conn.CreateCommand();
                        find.CommandText = @"SELECT Id FROM Products WHERE LOWER(Name)=LOWER($n) 
                                             AND (LOWER(Company)=LOWER($c) OR $c IS NULL OR $c='') LIMIT 1";
                        find.Parameters.AddWithValue("$n", item.ProductName?.Trim() ?? "");
                        find.Parameters.AddWithValue("$c", item.Company?.Trim() ?? "");
                        var found = find.ExecuteScalar();

                        if (found != null && found != DBNull.Value)
                        {
                            productId = Convert.ToInt32((long)found);
                        }
                    }

                    // Update existing product's purchase price, batch, expiry if applicable
                    if (productId > 0)
                    {
                        var upP = conn.CreateCommand();
                        upP.CommandText = @"UPDATE Products SET PurchasePrice=$pp, 
                                            SalePrice=CASE WHEN $sp > 0 THEN $sp ELSE SalePrice END,
                                            BatchNo=CASE WHEN $b <> '' THEN $b ELSE BatchNo END,
                                            ExpiryDate=CASE WHEN $e IS NOT NULL AND $e <> '' THEN $e ELSE ExpiryDate END,
                                            HSN=CASE WHEN $hsn <> '' THEN $hsn ELSE HSN END
                                            WHERE Id=$id";
                        upP.Parameters.AddWithValue("$pp", item.PurchasePrice);
                        upP.Parameters.AddWithValue("$sp", item.SellingPrice);
                        upP.Parameters.AddWithValue("$b", item.BatchNumber?.Trim() ?? "");
                        upP.Parameters.AddWithValue("$e", item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                        upP.Parameters.AddWithValue("$hsn", item.HSN?.Trim() ?? "");
                        upP.Parameters.AddWithValue("$id", productId);
                        upP.ExecuteNonQuery();
                    }

                    // Insert PurchaseItem
                    var icmd = conn.CreateCommand();
                    icmd.CommandText = @"INSERT INTO PurchaseItems (PurchaseId, ProductId, ProductName, Company, BatchNumber, ExpiryDate, Quantity, FreeQuantity, PurchasePrice, GST, Amount, HSN, CategoryName)
                        VALUES ($pid,$prid,$pn,$co,$bn,$ed,$q,$fq,$pp,$gst,$a,$hsn,$cat);";
                    icmd.Parameters.AddWithValue("$pid", purchaseId);
                    icmd.Parameters.AddWithValue("$prid", productId > 0 ? (object)productId : DBNull.Value);
                    icmd.Parameters.AddWithValue("$pn", item.ProductName?.Trim() ?? "");
                    icmd.Parameters.AddWithValue("$co", item.Company?.Trim() ?? "");
                    icmd.Parameters.AddWithValue("$bn", item.BatchNumber?.Trim() ?? "");
                    icmd.Parameters.AddWithValue("$ed", item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                    icmd.Parameters.AddWithValue("$q", item.Quantity);
                    icmd.Parameters.AddWithValue("$fq", item.FreeQuantity);
                    icmd.Parameters.AddWithValue("$pp", item.PurchasePrice);
                    icmd.Parameters.AddWithValue("$gst", item.GST);
                    icmd.Parameters.AddWithValue("$a", item.Amount);
                    icmd.Parameters.AddWithValue("$hsn", item.HSN?.Trim() ?? "");
                    icmd.Parameters.AddWithValue("$cat", item.CategoryName?.Trim() ?? "");
                    icmd.ExecuteNonQuery();

                    // update product stock ONLY if product is linked to catalog (Quantity + FreeQuantity)
                    if (productId > 0)
                    {
                        AdjustStock(productId, item.Quantity + item.FreeQuantity, conn);
                    }
                }

                tx.Commit();
                return purchaseId;
            }
            catch (Exception ex)
            {
                tx.Rollback();

                try
                {
                    // If a SQLite foreign key error occurred, run PRAGMA foreign_key_check to get details
                    if (ex.GetType().Name == "SqliteException")
                    {
                        var details = new List<string>();
                        var fkCmd = conn.CreateCommand();
                        fkCmd.CommandText = "PRAGMA foreign_key_check;";
                        using var fkR = fkCmd.ExecuteReader();
                        while (fkR.Read())
                        {
                            // pragma returns: table, rowid, parent, fk
                            var table = fkR.IsDBNull(0) ? "" : fkR.GetString(0);
                            var rowid = fkR.IsDBNull(1) ? "" : fkR.GetValue(1).ToString();
                            var parent = fkR.IsDBNull(2) ? "" : fkR.GetString(2);
                            details.Add($"Table={table}, RowId={rowid}, Parent={parent}");
                        }

                        if (details.Count > 0)
                        {
                            throw new Exception($"SQLite foreign key constraint failed. Details: {string.Join("; ", details)}\nOriginal: {ex.Message}", ex);
                        }
                    }
                }
                catch
                {
                    // ignore any errors while trying to fetch fk details
                }

                throw;
            }
        }

        public static (List<Purchase> Items, int Total) GetPurchasesPaged(string search = null, int page = 1, int pageSize = 20)
        {
            var list = new List<Purchase>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(p.PurchaseNumber LIKE $s OR s.Name LIKE $s OR p.SupplierInvoiceNumber LIKE $s OR p.PaperBillNumber LIKE $s)";

            var cnt = conn.CreateCommand();
            cnt.CommandText = $"SELECT COUNT(*) FROM Purchases p LEFT JOIN Suppliers s ON p.SupplierId=s.Id WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cnt.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            var total = Convert.ToInt32((long)cnt.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var data = conn.CreateCommand();
            data.CommandText = $"SELECT p.*, s.Name as SupplierName FROM Purchases p LEFT JOIN Suppliers s ON p.SupplierId=s.Id WHERE {where} ORDER BY p.PurchaseId DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) data.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            data.Parameters.AddWithValue("$limit", pageSize);
            data.Parameters.AddWithValue("$offset", offset);
            using var r = data.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Purchase
                {
                    PurchaseId = r.GetInt32(r.GetOrdinal("PurchaseId")),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    SupplierId = r["SupplierId"] is DBNull ? 0 : Convert.ToInt32(r["SupplierId"]),
                    SupplierName = r["SupplierName"]?.ToString(),
                    SupplierInvoiceNumber = r["SupplierInvoiceNumber"]?.ToString(),
                    PaperBillNumber = r["PaperBillNumber"]?.ToString(),
                    PurchaseDate = string.IsNullOrWhiteSpace(r["PurchaseDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["PurchaseDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    RoundOff = r["RoundOff"] is DBNull ? 0 : Convert.ToDecimal(r["RoundOff"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    PaidAmount = r["PaidAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PaidAmount"]),
                    PayableAmount = r["PayableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PayableAmount"]),
                    PaymentMethod = r["PaymentMethod"]?.ToString(),
                    PaymentReference = r["PaymentReference"]?.ToString()
                });
            }
            return (list, total);
        }

        public static List<string> GetSupplierNameSuggestions(string search)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(search)) return list;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Name FROM Suppliers WHERE Name LIKE $s ORDER BY Name LIMIT 10";
            cmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.GetString(0);
                if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
            }
            return list;
        }

        public static bool IsSupplierInvoiceNoExists(string supplierInvoiceNo, int excludePurchaseId = 0)
        {
            if (string.IsNullOrWhiteSpace(supplierInvoiceNo)) return false;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Purchases WHERE LOWER(TRIM(SupplierInvoiceNumber)) = LOWER(TRIM($no)) AND ($ex = 0 OR PurchaseId != $ex)";
            cmd.Parameters.AddWithValue("$no", supplierInvoiceNo.Trim());
            cmd.Parameters.AddWithValue("$ex", excludePurchaseId);
            var count = Convert.ToInt32((long)cmd.ExecuteScalar());
            return count > 0;
        }

        public static string GenerateNextBatchNumber()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(PurchaseItemId), 0) FROM PurchaseItems";
            var count = Convert.ToInt32((long)cmd.ExecuteScalar()) + 1;
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            return $"BAT-{dateStr}-{count:D4}";
        }

        public static List<Product> GetProductSuggestions(string search)
        {
            var list = new List<Product>();
            if (string.IsNullOrWhiteSpace(search)) return list;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.*, c.Name as CategoryName FROM Products p 
                                LEFT JOIN Categories c ON p.CategoryId = c.Id 
                                WHERE (p.Name LIKE $s OR p.Company LIKE $s OR p.ProductCode LIKE $s) 
                                ORDER BY p.Name LIMIT 10";
            cmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ProductCode = r["ProductCode"]?.ToString(),
                    Name = r["Name"]?.ToString(),
                    CategoryId = r["CategoryId"] is DBNull ? 0 : Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    PackSize = r["PackSize"] is DBNull ? 0 : Convert.ToDecimal(r["PackSize"]),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Unit = r["Unit"]?.ToString(),
                    PurchasePrice = r["PurchasePrice"] is DBNull ? 0 : Convert.ToDecimal(r["PurchasePrice"]),
                    SalePrice = r["SalePrice"] is DBNull ? 0 : Convert.ToDecimal(r["SalePrice"]),
                    GstPercent = r["GstPercent"] is DBNull ? 0 : Convert.ToDecimal(r["GstPercent"]),
                    StockQty = r["StockQty"] is DBNull ? 0 : Convert.ToInt32(r["StockQty"]),
                    HSN = r["HSN"]?.ToString()
                });
            }
            return list;
        }

        public static Purchase GetPurchaseById(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT p.*, s.Name as SupplierName FROM Purchases p LEFT JOIN Suppliers s ON p.SupplierId=s.Id WHERE p.PurchaseId=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new Purchase
                {
                    PurchaseId = r.GetInt32(r.GetOrdinal("PurchaseId")),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    SupplierId = r["SupplierId"] is DBNull ? 0 : Convert.ToInt32(r["SupplierId"]),
                    SupplierName = r["SupplierName"]?.ToString(),
                    SupplierInvoiceNumber = r["SupplierInvoiceNumber"]?.ToString(),
                    PaperBillNumber = r["PaperBillNumber"]?.ToString(),
                    PurchaseDate = string.IsNullOrWhiteSpace(r["PurchaseDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["PurchaseDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    PaidAmount = r["PaidAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PaidAmount"]),
                    PayableAmount = r["PayableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PayableAmount"]),
                    PaymentMethod = r["PaymentMethod"]?.ToString(),
                    PaymentReference = r["PaymentReference"]?.ToString()
                };
            }
            return null;
        }

        public static List<PurchaseItem> GetPurchaseItems(int purchaseId)
        {
            var list = new List<PurchaseItem>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PurchaseItems WHERE PurchaseId=$id";
            cmd.Parameters.AddWithValue("$id", purchaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PurchaseItem
                {
                    PurchaseItemId = r.GetInt32(r.GetOrdinal("PurchaseItemId")),
                    PurchaseId = r["PurchaseId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseId"]),
                    ProductId = r["ProductId"] is DBNull ? 0 : Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    BatchNumber = r["BatchNumber"]?.ToString(),
                    ExpiryDate = string.IsNullOrWhiteSpace(r["ExpiryDate"]?.ToString()) ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Quantity = r["Quantity"] is DBNull ? 0 : Convert.ToInt32(r["Quantity"]),
                    FreeQuantity = r["FreeQuantity"] is DBNull ? 0 : Convert.ToInt32(r["FreeQuantity"]),
                    PurchasePrice = r["PurchasePrice"] is DBNull ? 0 : Convert.ToDecimal(r["PurchasePrice"]),
                    GST = r["GST"] is DBNull ? 0 : Convert.ToDecimal(r["GST"]),
                    Amount = r["Amount"] is DBNull ? 0 : Convert.ToDecimal(r["Amount"])
                });
            }
            return list;
        }

        // Purchases table not implemented in this project. Return empty series for purchases.
        public static List<(string Label, decimal Value)> GetPurchaseSeries(DateTime start, DateTime end, string period)
        {
            // No purchases recorded - return zeroed series matching sales labels where possible.
            return new List<(string, decimal)>();
        }

        public static List<Product> GetExpiringProductsNextDays(int days)
        {
            var list = new List<Product>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.*, c.Name as CategoryName FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                WHERE p.ExpiryDate IS NOT NULL AND p.ExpiryDate <> ''
                    AND date(p.ExpiryDate) >= date('now','localtime')
                    AND date(p.ExpiryDate) <= date('now','localtime', $days)
                ORDER BY date(p.ExpiryDate) ASC";
            cmd.Parameters.AddWithValue("$days", $"+{days} days");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    Name = r["Name"].ToString(),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = string.IsNullOrWhiteSpace(r["ExpiryDate"]?.ToString()) ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    ReorderLevel = Convert.ToInt32(r["ReorderLevel"]),
                    CategoryName = r["CategoryName"]?.ToString()
                });
            }
            return list;
        }

        public static void Initialize()
        {
            bool isNew = !File.Exists(DbFile);
            using var conn = GetConnection();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    FullName TEXT,
                    ShopName TEXT,
                    ShopAddress TEXT,
                    ShopPhone TEXT,
                    Role TEXT DEFAULT 'Admin'
                );

                CREATE TABLE IF NOT EXISTS Categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT UNIQUE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ProductCode TEXT UNIQUE,
                    CategoryId INTEGER,
                    Company TEXT,
                    PackSize REAL DEFAULT 0,
                    BatchNo TEXT,
                    ExpiryDate TEXT,
                    Unit TEXT,
                    PurchasePrice REAL DEFAULT 0,
                    SalePrice REAL DEFAULT 0,
                    GstPercent REAL DEFAULT 0,
                    StockQty INTEGER DEFAULT 0,
                    PurchaseStockQty INTEGER DEFAULT 0,
                    ReorderLevel INTEGER DEFAULT 5,
                    HSN TEXT,
                    Status INTEGER DEFAULT 1,
                    FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
                );

                CREATE TABLE IF NOT EXISTS Customers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Phone TEXT,
                    Address TEXT,
                    GSTIN TEXT
                );

                CREATE TABLE IF NOT EXISTS Invoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InvoiceNo TEXT UNIQUE NOT NULL,
                    CustomerId INTEGER,
                    InvoiceDate TEXT,
                    SubTotal REAL,
                    GstAmount REAL,
                    GrandTotal REAL,
                    FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
                );

                CREATE TABLE IF NOT EXISTS InvoiceItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InvoiceId INTEGER,
                    ProductId INTEGER,
                    Qty INTEGER,
                    Rate REAL,
                    GstPercent REAL,
                    Amount REAL,
                    FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id),
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                -- Suppliers table used by Purchase module (added by Purchase Management)
                CREATE TABLE IF NOT EXISTS Suppliers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Phone TEXT,
                    Address TEXT,
                    GSTIN TEXT
                );

                -- Purchase header
                CREATE TABLE IF NOT EXISTS Purchases (
                    PurchaseId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PurchaseNumber TEXT UNIQUE NOT NULL,
                    SupplierId INTEGER,
                    SupplierInvoiceNumber TEXT,
                    PaperBillNumber TEXT,
                    PurchaseDate TEXT,
                    SubTotal REAL,
                    Discount REAL,
                    TaxableAmount REAL,
                    GSTAmount REAL,
                    RoundOff REAL,
                    GrandTotal REAL,
                    PaidAmount REAL,
                    PayableAmount REAL,
                    PaymentMethod TEXT,
                    PaymentReference TEXT,
                    CreatedAt TEXT,
                    FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)
                );

                -- Purchase items (batches are recorded per item)
                CREATE TABLE IF NOT EXISTS PurchaseItems (
                    PurchaseItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PurchaseId INTEGER,
                    ProductId INTEGER,
                    ProductName TEXT,
                    Company TEXT,
                    BatchNumber TEXT,
                    ExpiryDate TEXT,
                    Quantity INTEGER,
                    FreeQuantity INTEGER,
                    PurchasePrice REAL,
                    GST REAL,
                    Amount REAL,
                    HSN TEXT,
                    CategoryName TEXT,
                    FOREIGN KEY (PurchaseId) REFERENCES Purchases(PurchaseId),
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                CREATE TABLE IF NOT EXISTS Farmers (
                    FarmerId INTEGER PRIMARY KEY AUTOINCREMENT,
                    FarmerName TEXT NOT NULL,
                    MobileNumber TEXT,
                    VillageName TEXT,
                    Status INTEGER DEFAULT 1,
                    CreatedDate TEXT,
                    UpdatedDate TEXT
                );

                CREATE TABLE IF NOT EXISTS PurchaseReturns (
                    PurchaseReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReturnNumber TEXT UNIQUE NOT NULL,
                    PurchaseId INTEGER,
                    SupplierId INTEGER,
                    SupplierInvoiceNumber TEXT,
                    PaperBillNumber TEXT,
                    ReturnDate TEXT,
                    SubTotal REAL,
                    Discount REAL,
                    TaxableAmount REAL,
                    GSTAmount REAL,
                    RoundOff REAL,
                    GrandTotal REAL,
                    ReturnReason TEXT,
                    Notes TEXT,
                    Status TEXT DEFAULT 'Completed',
                    CreatedAt TEXT,
                    FOREIGN KEY (PurchaseId) REFERENCES Purchases(PurchaseId),
                    FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)
                );

                CREATE TABLE IF NOT EXISTS PurchaseReturnItems (
                    PurchaseReturnItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PurchaseReturnId INTEGER,
                    PurchaseItemId INTEGER,
                    ProductId INTEGER,
                    ProductName TEXT,
                    Company TEXT,
                    BatchNumber TEXT,
                    ExpiryDate TEXT,
                    PurchasedQuantity INTEGER,
                    AlreadyReturnedQuantity INTEGER,
                    ReturnableQuantity INTEGER,
                    ReturnQuantity INTEGER,
                    PurchasePrice REAL,
                    GST REAL,
                    Amount REAL,
                    FOREIGN KEY (PurchaseReturnId) REFERENCES PurchaseReturns(PurchaseReturnId),
                    FOREIGN KEY (PurchaseItemId) REFERENCES PurchaseItems(PurchaseItemId),
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                CREATE TABLE IF NOT EXISTS SalesReturns (
                    SalesReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReturnNumber TEXT UNIQUE NOT NULL,
                    InvoiceId INTEGER,
                    InvoiceNo TEXT,
                    FarmerId INTEGER,
                    FarmerName TEXT,
                    MobileNumber TEXT,
                    VillageName TEXT,
                    ReturnDate TEXT,
                    SubTotal REAL,
                    Discount REAL,
                    TaxableAmount REAL,
                    GSTAmount REAL,
                    RoundOff REAL,
                    GrandTotal REAL,
                    AdjustmentType TEXT,
                    ReturnReason TEXT,
                    Notes TEXT,
                    Status TEXT DEFAULT 'Completed',
                    CreatedAt TEXT,
                    FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id),
                    FOREIGN KEY (FarmerId) REFERENCES Farmers(FarmerId)
                );

                CREATE TABLE IF NOT EXISTS SalesReturnItems (
                    SalesReturnItemId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SalesReturnId INTEGER,
                    InvoiceItemId INTEGER,
                    ProductId INTEGER,
                    ProductName TEXT,
                    Company TEXT,
                    BatchNumber TEXT,
                    ExpiryDate TEXT,
                    PurchasedQuantity INTEGER,
                    AlreadyReturnedQuantity INTEGER,
                    ReturnableQuantity INTEGER,
                    ReturnQuantity INTEGER,
                    Rate REAL,
                    GstPercent REAL,
                    Amount REAL,
                    FOREIGN KEY (SalesReturnId) REFERENCES SalesReturns(SalesReturnId),
                    FOREIGN KEY (InvoiceItemId) REFERENCES InvoiceItems(Id),
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );

                CREATE TABLE IF NOT EXISTS PaymentReceipts (
                    PaymentReceiptId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReceiptNumber TEXT UNIQUE NOT NULL,
                    FarmerId INTEGER,
                    FarmerName TEXT,
                    MobileNumber TEXT,
                    VillageName TEXT,
                    ReceiptDate TEXT,
                    OpeningBalance REAL,
                    ReceivedAmount REAL,
                    ClosingBalance REAL,
                    PaymentMode TEXT,
                    TransactionReference TEXT,
                    ChequeNumber TEXT,
                    ChequeDate TEXT,
                    BankName TEXT,
                    Notes TEXT,
                    CreatedAt TEXT,
                    FOREIGN KEY (FarmerId) REFERENCES Farmers(FarmerId)
                );

                CREATE TABLE IF NOT EXISTS PaymentReceiptAllocations (
                    PaymentReceiptAllocationId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PaymentReceiptId INTEGER,
                    InvoiceId INTEGER,
                    AllocatedAmount REAL,
                    CreatedAt TEXT,
                    FOREIGN KEY (PaymentReceiptId) REFERENCES PaymentReceipts(PaymentReceiptId),
                    FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id)
                );

                CREATE TABLE IF NOT EXISTS CompanySettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShopName TEXT NOT NULL,
                    ShopAddress TEXT,
                    ShopPhone TEXT,
                    GSTIN TEXT,
                    LicenseNumber TEXT,
                    BankName TEXT,
                    AccountName TEXT,
                    AccountNumber TEXT,
                    IFSCCode TEXT,
                    UpiId TEXT,
                    TermsAndConditions TEXT,
                    FooterMessage TEXT,
                    UpdatedAt TEXT
                );

                CREATE TABLE IF NOT EXISTS StockAdjustments (
                    AdjustmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL,
                    ProductName TEXT,
                    BatchNumber TEXT,
                    PreviousQty INTEGER NOT NULL,
                    NewQty INTEGER NOT NULL,
                    DeltaQty INTEGER NOT NULL,
                    AdjustmentType TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    Notes TEXT,
                    AdjustedBy TEXT,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (ProductId) REFERENCES Products(Id)
                );
            ";
            cmd.ExecuteNonQuery();

            // Ensure Invoice table has PaymentMethod column for revenue breakdown (safe default 'Cash').
            var colCmd = conn.CreateCommand();
            colCmd.CommandText = "PRAGMA table_info(Invoices);";
            using var reader = colCmd.ExecuteReader();
            bool hasPaymentMethod = false;
            while (reader.Read())
            {
                if (reader.GetString(reader.GetOrdinal("name")) == "PaymentMethod") { hasPaymentMethod = true; break; }
            }
            if (!hasPaymentMethod)
            {
                var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Invoices ADD COLUMN PaymentMethod TEXT DEFAULT 'Cash';";
                alter.ExecuteNonQuery();
            }

            // Ensure Products table has columns added by newer app versions.
            var prodInfoCmd = conn.CreateCommand();
            prodInfoCmd.CommandText = "PRAGMA table_info(Products);";
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var rc = prodInfoCmd.ExecuteReader())
            {
                while (rc.Read()) existingCols.Add(rc.GetString(rc.GetOrdinal("name")));
            }

            var alterCmd = conn.CreateCommand();
            // Add missing columns safely (SQLite supports ALTER TABLE ADD COLUMN)
            if (!existingCols.Contains("ProductCode"))
            {
                alterCmd.CommandText = "ALTER TABLE Products ADD COLUMN ProductCode TEXT UNIQUE;";
                alterCmd.ExecuteNonQuery();
            }
            if (!existingCols.Contains("PackSize"))
            {
                alterCmd.CommandText = "ALTER TABLE Products ADD COLUMN PackSize REAL DEFAULT 0;";
                alterCmd.ExecuteNonQuery();
            }
            if (!existingCols.Contains("PurchaseStockQty"))
            {
                alterCmd.CommandText = "ALTER TABLE Products ADD COLUMN PurchaseStockQty INTEGER DEFAULT 0;";
                alterCmd.ExecuteNonQuery();
            }
            if (!existingCols.Contains("HSN"))
            {
                alterCmd.CommandText = "ALTER TABLE Products ADD COLUMN HSN TEXT;";
                alterCmd.ExecuteNonQuery();
            }
            if (!existingCols.Contains("Status"))
            {
                alterCmd.CommandText = "ALTER TABLE Products ADD COLUMN Status INTEGER DEFAULT 1;";
                alterCmd.ExecuteNonQuery();
            }

            // Ensure Invoices table has all new billing columns
            var invInfoCmd = conn.CreateCommand();
            invInfoCmd.CommandText = "PRAGMA table_info(Invoices);";
            var existingInvCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var rc = invInfoCmd.ExecuteReader())
            {
                while (rc.Read()) existingInvCols.Add(rc.GetString(rc.GetOrdinal("name")));
            }
            if (!existingInvCols.Contains("PaperBillNo")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN PaperBillNo TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("CustomerName")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN CustomerName TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("Discount")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN Discount REAL DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("TaxableAmount")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN TaxableAmount REAL DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("RoundOff")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN RoundOff REAL DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("PaidAmount")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN PaidAmount REAL DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("PayableAmount")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN PayableAmount REAL DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("PaymentReference")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN PaymentReference TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("Notes")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN Notes TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("FarmerId")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN FarmerId INTEGER DEFAULT 0;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("MobileNumber")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN MobileNumber TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("VillageName")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN VillageName TEXT;"; a.ExecuteNonQuery(); }
            if (!existingInvCols.Contains("Status")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE Invoices ADD COLUMN Status TEXT DEFAULT 'Active';"; a.ExecuteNonQuery(); }

            // Ensure InvoiceItems table has batch, expiry, unit, company, HSN, product name columns
            var iiInfoCmd = conn.CreateCommand();
            iiInfoCmd.CommandText = "PRAGMA table_info(InvoiceItems);";
            var existingIiCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var rc = iiInfoCmd.ExecuteReader())
            {
                while (rc.Read()) existingIiCols.Add(rc.GetString(rc.GetOrdinal("name")));
            }
            if (!existingIiCols.Contains("ProductName")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN ProductName TEXT;"; a.ExecuteNonQuery(); }
            if (!existingIiCols.Contains("BatchNo")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN BatchNo TEXT;"; a.ExecuteNonQuery(); }
            if (!existingIiCols.Contains("ExpiryDate")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN ExpiryDate TEXT;"; a.ExecuteNonQuery(); }
            if (!existingIiCols.Contains("HSN")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN HSN TEXT;"; a.ExecuteNonQuery(); }
            if (!existingIiCols.Contains("Unit")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN Unit TEXT;"; a.ExecuteNonQuery(); }
            if (!existingIiCols.Contains("Company")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE InvoiceItems ADD COLUMN Company TEXT;"; a.ExecuteNonQuery(); }

            // Ensure PurchaseItems table has HSN and CategoryName columns
            var piInfoCmd = conn.CreateCommand();
            piInfoCmd.CommandText = "PRAGMA table_info(PurchaseItems);";
            var existingPiCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var rc = piInfoCmd.ExecuteReader())
            {
                while (rc.Read()) existingPiCols.Add(rc.GetString(rc.GetOrdinal("name")));
            }
            if (!existingPiCols.Contains("HSN")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE PurchaseItems ADD COLUMN HSN TEXT;"; a.ExecuteNonQuery(); }
            if (!existingPiCols.Contains("CategoryName")) { var a = conn.CreateCommand(); a.CommandText = "ALTER TABLE PurchaseItems ADD COLUMN CategoryName TEXT;"; a.ExecuteNonQuery(); }

            if (isNew)
            {
                SeedDefaultData(conn);
            }
        }

        private static void SeedDefaultData(SqliteConnection conn)
        {
            // Default categories relevant to a seeds & pesticides shop.
            string[] categories = { "Seeds", "Pesticides", "Fertilizers", "Tools" };
            foreach (var cat in categories)
            {
                var c = conn.CreateCommand();
                c.CommandText = "INSERT OR IGNORE INTO Categories (Name) VALUES ($n)";
                c.Parameters.AddWithValue("$n", cat);
                c.ExecuteNonQuery();
            }

            // Default admin login - shop owner should change this after first login.
            var userCmd = conn.CreateCommand();
            userCmd.CommandText = @"INSERT OR IGNORE INTO Users
                (Username, PasswordHash, FullName, ShopName, ShopAddress, ShopPhone, Role)
                VALUES ($u, $p, $f, $s, $a, $ph, 'Admin')";
            userCmd.Parameters.AddWithValue("$u", "admin");
            userCmd.Parameters.AddWithValue("$p", HashPassword("admin123"));
            userCmd.Parameters.AddWithValue("$f", "Shop Owner");
            userCmd.Parameters.AddWithValue("$s", "My Krushi Kendra");
            userCmd.Parameters.AddWithValue("$a", "Your shop address here");
            userCmd.Parameters.AddWithValue("$ph", "9999999999");
            userCmd.ExecuteNonQuery();

            // Seed SuperAdmin login 'yatin' / 'yatin123'
            var yatinCmd = conn.CreateCommand();
            yatinCmd.CommandText = @"INSERT OR IGNORE INTO Users
                (Username, PasswordHash, FullName, ShopName, ShopAddress, ShopPhone, Role)
                VALUES ($u, $p, $f, $s, $a, $ph, 'SuperAdmin')";
            yatinCmd.Parameters.AddWithValue("$u", "yatin");
            yatinCmd.Parameters.AddWithValue("$p", HashPassword("yatin123"));
            yatinCmd.Parameters.AddWithValue("$f", "Yatin Owner");
            yatinCmd.Parameters.AddWithValue("$s", "My Krushi Kendra");
            yatinCmd.Parameters.AddWithValue("$a", "Your shop address here");
            yatinCmd.Parameters.AddWithValue("$ph", "9999999999");
            yatinCmd.ExecuteNonQuery();
        }

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // ---------------- Authentication ----------------

        public static User ValidateLogin(string username, string password)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Users WHERE Username = $u AND PasswordHash = $p";
            cmd.Parameters.AddWithValue("$u", username);
            cmd.Parameters.AddWithValue("$p", HashPassword(password));
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new User
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    Username = r.GetString(r.GetOrdinal("Username")),
                    FullName = r["FullName"]?.ToString(),
                    ShopName = r["ShopName"]?.ToString(),
                    ShopAddress = r["ShopAddress"]?.ToString(),
                    ShopPhone = r["ShopPhone"]?.ToString(),
                    Role = r["Role"]?.ToString()
                };
            }
            return null;
        }

        // ---------------- Categories ----------------

        public static List<Category> GetCategories()
        {
            var list = new List<Category>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name FROM Categories ORDER BY Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Category { Id = r.GetInt32(0), Name = r.GetString(1) });
            return list;
        }

        // ---------------- Products ----------------

        public static List<Product> GetProducts(string search = null)
        {
            var list = new List<Product>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.*, c.Name as CategoryName FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                WHERE ($s IS NULL OR p.Name LIKE $s OR p.Company LIKE $s OR p.ProductCode LIKE $s OR p.BatchNo LIKE $s OR p.HSN LIKE $s)
                ORDER BY p.Name";
            cmd.Parameters.AddWithValue("$s", string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : $"%{search}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ProductCode = r["ProductCode"]?.ToString(),
                    Name = r["Name"].ToString(),
                    CategoryId = r["CategoryId"] is DBNull ? 0 : Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    PackSize = r["PackSize"] is DBNull ? 0 : Convert.ToDecimal(r["PackSize"]),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Unit = r["Unit"]?.ToString(),
                    PurchasePrice = Convert.ToDecimal(r["PurchasePrice"]),
                    SalePrice = Convert.ToDecimal(r["SalePrice"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    PurchaseStockQty = r["PurchaseStockQty"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseStockQty"]),
                    ReorderLevel = Convert.ToInt32(r["ReorderLevel"]),
                    HSN = r["HSN"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                });
            }
            return list;
        }

        /// <summary>
        /// Returns only products that have stock available (StockQty > 0).
        /// Used in Billing to ensure only purchased products with available stock can be selected.
        /// </summary>
        public static List<Product> GetProductsInStock(string search = null)
        {
            var list = new List<Product>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.*, c.Name as CategoryName FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                WHERE p.StockQty > 0 AND p.Status = 1
                AND ($s IS NULL OR p.Name LIKE $s OR p.Company LIKE $s OR p.ProductCode LIKE $s OR p.BatchNo LIKE $s)
                ORDER BY p.Name";
            cmd.Parameters.AddWithValue("$s", string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : $"%{search}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ProductCode = r["ProductCode"]?.ToString(),
                    Name = r["Name"].ToString(),
                    CategoryId = r["CategoryId"] is DBNull ? 0 : Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    PackSize = r["PackSize"] is DBNull ? 0 : Convert.ToDecimal(r["PackSize"]),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Unit = r["Unit"]?.ToString(),
                    PurchasePrice = Convert.ToDecimal(r["PurchasePrice"]),
                    SalePrice = Convert.ToDecimal(r["SalePrice"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    PurchaseStockQty = r["PurchaseStockQty"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseStockQty"]),
                    ReorderLevel = Convert.ToInt32(r["ReorderLevel"]),
                    HSN = r["HSN"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                });
            }
            return list;
        }

        public static void SaveProduct(Product p)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            // basic validation: ensure product code is unique when inserting/updating
            if (!string.IsNullOrWhiteSpace(p.ProductCode))
            {
                var existsCmd = conn.CreateCommand();
                existsCmd.CommandText = "SELECT COUNT(1) FROM Products WHERE ProductCode=$pc" + (p.Id == 0 ? "" : " AND Id<>$id");
                existsCmd.Parameters.AddWithValue("$pc", p.ProductCode);
                if (p.Id != 0) existsCmd.Parameters.AddWithValue("$id", p.Id);
                var exists = Convert.ToInt32((long)existsCmd.ExecuteScalar());
                if (exists > 0) throw new Exception("Product code already exists.");
            }
            if (p.Id == 0)
            {
                cmd.CommandText = @"INSERT INTO Products
                    (ProductCode, Name, CategoryId, Company, PackSize, BatchNo, ExpiryDate, Unit, PurchasePrice, SalePrice, GstPercent, StockQty, PurchaseStockQty, ReorderLevel, HSN, Status)
                    VALUES ($pc,$n,$c,$co,$ps,$b,$e,$u,$pp,$sp,$g,$sq,$psq,$rl,$hsn,$st)";
                // Execute insert and fetch last inserted id to update the product object's Id
                cmd.Parameters.AddWithValue("$n", p.Name);
                cmd.Parameters.AddWithValue("$pc", (object)p.ProductCode ?? "");
                cmd.Parameters.AddWithValue("$c", p.CategoryId);
                cmd.Parameters.AddWithValue("$co", (object)p.Company ?? "");
                cmd.Parameters.AddWithValue("$ps", p.PackSize);
                cmd.Parameters.AddWithValue("$b", (object)p.BatchNo ?? "");
                cmd.Parameters.AddWithValue("$e", p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$u", (object)p.Unit ?? "");
                cmd.Parameters.AddWithValue("$pp", p.PurchasePrice);
                cmd.Parameters.AddWithValue("$sp", p.SalePrice);
                cmd.Parameters.AddWithValue("$g", p.GstPercent);
                cmd.Parameters.AddWithValue("$sq", p.StockQty);
                cmd.Parameters.AddWithValue("$psq", p.PurchaseStockQty);
                cmd.Parameters.AddWithValue("$rl", p.ReorderLevel);
                cmd.Parameters.AddWithValue("$hsn", (object)p.HSN ?? "");
                cmd.Parameters.AddWithValue("$st", p.Status);
                cmd.ExecuteNonQuery();
                // get last id
                var idCmd = conn.CreateCommand();
                idCmd.CommandText = "SELECT last_insert_rowid();";
                var last = idCmd.ExecuteScalar();
                if (last != null && last is long) p.Id = Convert.ToInt32((long)last);
                return;
            }
            else
            {
                cmd.CommandText = @"UPDATE Products SET ProductCode=$pc, Name=$n, CategoryId=$c, Company=$co, PackSize=$ps, BatchNo=$b,
                    ExpiryDate=$e, Unit=$u, PurchasePrice=$pp, SalePrice=$sp, GstPercent=$g,
                    StockQty=$sq, PurchaseStockQty=$psq, ReorderLevel=$rl, HSN=$hsn, Status=$st WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", p.Id);
            }
            // (uniqueness already validated above)
            cmd.Parameters.AddWithValue("$n", p.Name);
            cmd.Parameters.AddWithValue("$pc", (object)p.ProductCode ?? "");
            cmd.Parameters.AddWithValue("$c", p.CategoryId);
            cmd.Parameters.AddWithValue("$co", (object)p.Company ?? "");
            cmd.Parameters.AddWithValue("$ps", p.PackSize);
            cmd.Parameters.AddWithValue("$b", (object)p.BatchNo ?? "");
            cmd.Parameters.AddWithValue("$e", p.ExpiryDate.HasValue ? p.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$u", (object)p.Unit ?? "");
            cmd.Parameters.AddWithValue("$pp", p.PurchasePrice);
            cmd.Parameters.AddWithValue("$sp", p.SalePrice);
            cmd.Parameters.AddWithValue("$g", p.GstPercent);
            cmd.Parameters.AddWithValue("$sq", p.StockQty);
            cmd.Parameters.AddWithValue("$psq", p.PurchaseStockQty);
            cmd.Parameters.AddWithValue("$rl", p.ReorderLevel);
            cmd.Parameters.AddWithValue("$hsn", (object)p.HSN ?? "");
            cmd.Parameters.AddWithValue("$st", p.Status);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteProduct(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Products WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static void SetProductStock(int id, int qty)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Products SET StockQty=$sq WHERE Id=$id";
            cmd.Parameters.AddWithValue("$sq", qty);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static Product GetProductById(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT p.*, c.Name as CategoryName FROM Products p LEFT JOIN Categories c ON p.CategoryId=c.Id WHERE p.Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ProductCode = r["ProductCode"]?.ToString(),
                    Name = r["Name"].ToString(),
                    CategoryId = r["CategoryId"] is DBNull ? 0 : Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    PackSize = r["PackSize"] is DBNull ? 0 : Convert.ToDecimal(r["PackSize"]),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Unit = r["Unit"]?.ToString(),
                    PurchasePrice = Convert.ToDecimal(r["PurchasePrice"]),
                    SalePrice = Convert.ToDecimal(r["SalePrice"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    PurchaseStockQty = r["PurchaseStockQty"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseStockQty"]),
                    ReorderLevel = r["ReorderLevel"] is DBNull ? 0 : Convert.ToInt32(r["ReorderLevel"]),
                    HSN = r["HSN"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                };
            }
            return null;
        }

        public static bool IsProductCodeExists(string productCode, int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return false;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Products WHERE ProductCode=$pc" + (excludeId > 0 ? " AND Id<>$id" : "");
            cmd.Parameters.AddWithValue("$pc", productCode);
            if (excludeId > 0) cmd.Parameters.AddWithValue("$id", excludeId);
            var cnt = Convert.ToInt32((long)cmd.ExecuteScalar());
            return cnt > 0;
        }

        // Paged products with filters
        public static (List<Product> Items, int Total) GetProductsPaged(string search = null, int categoryId = 0, string company = null, int status = -1, int page = 1, int pageSize = 20)
        {
            var list = new List<Product>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            // Count total
            var whereClauses = new List<string>();
            whereClauses.Add("p.StockQty > 0");
            if (!string.IsNullOrWhiteSpace(search)) whereClauses.Add("(p.Name LIKE $s OR p.Company LIKE $s OR p.ProductCode LIKE $s OR p.BatchNo LIKE $s OR p.HSN LIKE $s)");
            if (categoryId > 0) whereClauses.Add("p.CategoryId = $cat");
            if (!string.IsNullOrWhiteSpace(company)) whereClauses.Add("p.Company = $comp");
            if (status == 0 || status == 1) whereClauses.Add("p.Status = $st");
            var where = string.Join(" AND ", whereClauses);

            cmd.CommandText = $"SELECT COUNT(*) FROM Products p WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$s", $"%{search}%");
            if (categoryId > 0) cmd.Parameters.AddWithValue("$cat", categoryId);
            if (!string.IsNullOrWhiteSpace(company)) cmd.Parameters.AddWithValue("$comp", company);
            if (status == 0 || status == 1) cmd.Parameters.AddWithValue("$st", status);
            var total = Convert.ToInt32((long)cmd.ExecuteScalar());

            // fetch page
            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = $"SELECT p.*, c.Name as CategoryName FROM Products p LEFT JOIN Categories c ON p.CategoryId=c.Id WHERE {where} ORDER BY p.Name LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) dataCmd.Parameters.AddWithValue("$s", $"%{search}%");
            if (categoryId > 0) dataCmd.Parameters.AddWithValue("$cat", categoryId);
            if (!string.IsNullOrWhiteSpace(company)) dataCmd.Parameters.AddWithValue("$comp", company);
            if (status == 0 || status == 1) dataCmd.Parameters.AddWithValue("$st", status);
            dataCmd.Parameters.AddWithValue("$limit", pageSize);
            dataCmd.Parameters.AddWithValue("$offset", offset);
            using var r = dataCmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ProductCode = r["ProductCode"]?.ToString(),
                    Name = r["Name"].ToString(),
                    CategoryId = r["CategoryId"] is DBNull ? 0 : Convert.ToInt32(r["CategoryId"]),
                    CategoryName = r["CategoryName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    PackSize = r["PackSize"] is DBNull ? 0 : Convert.ToDecimal(r["PackSize"]),
                    BatchNo = r["BatchNo"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Unit = r["Unit"]?.ToString(),
                    PurchasePrice = Convert.ToDecimal(r["PurchasePrice"]),
                    SalePrice = Convert.ToDecimal(r["SalePrice"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    PurchaseStockQty = r["PurchaseStockQty"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseStockQty"]),
                    ReorderLevel = r["ReorderLevel"] is DBNull ? 0 : Convert.ToInt32(r["ReorderLevel"]),
                    HSN = r["HSN"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                });
            }
            return (list, total);
        }

        // Delete or mark inactive depending on transactions existence
        public static void DeleteOrDeactivateProduct(int id)
        {
            using var conn = GetConnection();
            // If product appears in InvoiceItems, keep for history and mark inactive
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM InvoiceItems WHERE ProductId=$id";
            checkCmd.Parameters.AddWithValue("$id", id);
            var cnt = Convert.ToInt32((long)checkCmd.ExecuteScalar());
            if (cnt > 0)
            {
                var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE Products SET Status=0 WHERE Id=$id";
                upd.Parameters.AddWithValue("$id", id);
                upd.ExecuteNonQuery();
            }
            else
            {
                var del = conn.CreateCommand();
                del.CommandText = "DELETE FROM Products WHERE Id=$id";
                del.Parameters.AddWithValue("$id", id);
                del.ExecuteNonQuery();
            }
        }

        public static void AdjustStock(int productId, int deltaQty, SqliteConnection existingConn = null)
        {
            var conn = existingConn ?? GetConnection();
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Products SET StockQty = StockQty + $d WHERE Id = $id";
                cmd.Parameters.AddWithValue("$d", deltaQty);
                cmd.Parameters.AddWithValue("$id", productId);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                if (existingConn == null) conn.Dispose();
            }
        }

        // ---------------- Customers ----------------

        public static List<Customer> GetCustomers(string search = null)
        {
            var list = new List<Customer>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM Customers
                WHERE ($s IS NULL OR Name LIKE $s OR Phone LIKE $s) ORDER BY Name";
            cmd.Parameters.AddWithValue("$s", string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : $"%{search}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Customer
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    Name = r["Name"].ToString(),
                    Phone = r["Phone"]?.ToString(),
                    Address = r["Address"]?.ToString(),
                    GSTIN = r["GSTIN"]?.ToString()
                });
            }
            return list;
        }

        public static void SaveCustomer(Customer c)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            if (c.Id == 0)
                cmd.CommandText = "INSERT INTO Customers (Name, Phone, Address, GSTIN) VALUES ($n,$p,$a,$g)";
            else
            {
                cmd.CommandText = "UPDATE Customers SET Name=$n, Phone=$p, Address=$a, GSTIN=$g WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", c.Id);
            }
            cmd.Parameters.AddWithValue("$n", c.Name);
            cmd.Parameters.AddWithValue("$p", (object)c.Phone ?? "");
            cmd.Parameters.AddWithValue("$a", (object)c.Address ?? "");
            cmd.Parameters.AddWithValue("$g", (object)c.GSTIN ?? "");
            cmd.ExecuteNonQuery();
        }

        public static void DeleteCustomer(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Customers WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // ---------------- Billing / Invoices ----------------

        public static string GenerateNextInvoiceNo()
        {
            try
            {
                using var conn = GetConnection();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Invoices";
                var res = cmd.ExecuteScalar();
                long count = res != null && res != DBNull.Value ? Convert.ToInt64(res) : 0;
                return $"INV-{DateTime.Now:yyyyMMdd}-{count + 1:0000}";
            }
            catch
            {
                return $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            }
        }

        public static int SaveInvoice(Invoice invoice, List<InvoiceItem> items)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Re-validate all items against live database state right inside the transaction
                foreach (var item in items)
                {
                    var chkCmd = conn.CreateCommand();
                    chkCmd.CommandText = "SELECT Name, StockQty, ExpiryDate FROM Products WHERE Id=$pid";
                    chkCmd.Parameters.AddWithValue("$pid", item.ProductId);
                    using var r = chkCmd.ExecuteReader();
                    if (!r.Read())
                    {
                        throw new Exception($"Product '{item.ProductName}' not found in inventory.");
                    }
                    string liveName = r["Name"]?.ToString() ?? item.ProductName;
                    int liveStock = Convert.ToInt32(r["StockQty"]);
                    DateTime? expDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString());
                    r.Close();

                    // Check expiry date
                    if (expDate.HasValue && expDate.Value.Date < DateTime.Today)
                    {
                        throw new Exception($"Product batch for '{liveName}' has expired ({expDate.Value:dd MMM yyyy}) and cannot be sold.");
                    }

                    // Check live stock quantity
                    if (item.Qty > liveStock)
                    {
                        throw new Exception($"Stock changed for '{liveName}'. Available quantity is now {liveStock}.");
                    }
                }

                // 2. Insert Invoice Header
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Invoices 
                    (InvoiceNo, PaperBillNo, CustomerId, FarmerId, CustomerName, MobileNumber, VillageName, InvoiceDate, SubTotal, Discount, TaxableAmount, GstAmount, RoundOff, GrandTotal, PaymentMethod, PaidAmount, PayableAmount, PaymentReference, Notes, Status)
                    VALUES ($no,$pbno,$cid,$fid,$cname,$mob,$vil,$dt,$sub,$disc,$tax,$gst,$ro,$grand,$pm,$paid,$payable,$pref,$notes,$st);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$no", invoice.InvoiceNo);
                cmd.Parameters.AddWithValue("$pbno", invoice.PaperBillNo ?? "");
                cmd.Parameters.AddWithValue("$cid", invoice.CustomerId > 0 ? (object)invoice.CustomerId : DBNull.Value);
                cmd.Parameters.AddWithValue("$fid", invoice.FarmerId);
                cmd.Parameters.AddWithValue("$cname", invoice.CustomerName ?? "");
                cmd.Parameters.AddWithValue("$mob", invoice.MobileNumber ?? "");
                cmd.Parameters.AddWithValue("$vil", invoice.VillageName ?? "");
                cmd.Parameters.AddWithValue("$dt", invoice.InvoiceDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$sub", invoice.SubTotal);
                cmd.Parameters.AddWithValue("$disc", invoice.Discount);
                cmd.Parameters.AddWithValue("$tax", invoice.TaxableAmount);
                cmd.Parameters.AddWithValue("$gst", invoice.GstAmount);
                cmd.Parameters.AddWithValue("$ro", invoice.RoundOff);
                cmd.Parameters.AddWithValue("$grand", invoice.GrandTotal);
                cmd.Parameters.AddWithValue("$pm", string.IsNullOrWhiteSpace(invoice.PaymentMethod) ? "Cash" : invoice.PaymentMethod);
                cmd.Parameters.AddWithValue("$paid", invoice.PaidAmount);
                cmd.Parameters.AddWithValue("$payable", invoice.PayableAmount);
                cmd.Parameters.AddWithValue("$pref", invoice.PaymentReference ?? "");
                cmd.Parameters.AddWithValue("$notes", invoice.Notes ?? "");
                cmd.Parameters.AddWithValue("$st", string.IsNullOrWhiteSpace(invoice.Status) ? "Active" : invoice.Status);
                int invoiceId = Convert.ToInt32((long)cmd.ExecuteScalar());

                // 3. Insert Invoice Items & Deduct Stock
                foreach (var item in items)
                {
                    var ic = conn.CreateCommand();
                    ic.CommandText = @"INSERT INTO InvoiceItems (InvoiceId, ProductId, ProductName, Company, BatchNo, ExpiryDate, Unit, Qty, Rate, GstPercent, Amount, HSN)
                        VALUES ($iid,$pid,$pn,$co,$bn,$ed,$u,$q,$r,$g,$a,$hsn)";
                    ic.Parameters.AddWithValue("$iid", invoiceId);
                    ic.Parameters.AddWithValue("$pid", item.ProductId);
                    ic.Parameters.AddWithValue("$pn", item.ProductName ?? "");
                    ic.Parameters.AddWithValue("$co", item.Company ?? "");
                    ic.Parameters.AddWithValue("$bn", item.BatchNo ?? "");
                    ic.Parameters.AddWithValue("$ed", item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                    ic.Parameters.AddWithValue("$u", item.Unit ?? "");
                    ic.Parameters.AddWithValue("$q", item.Qty);
                    ic.Parameters.AddWithValue("$r", item.Rate);
                    ic.Parameters.AddWithValue("$g", item.GstPercent);
                    ic.Parameters.AddWithValue("$a", item.Amount);
                    ic.Parameters.AddWithValue("$hsn", item.HSN ?? "");
                    ic.ExecuteNonQuery();

                    // Reduce stock for the sold batch item.
                    AdjustStock(item.ProductId, -item.Qty, conn);
                }

                tx.Commit();
                return invoiceId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                // If this is a SqliteException, gather FK details and rethrow a clearer message
                if (ex is SqliteException se)
                {
                    var msg = $"SQLite Error {se.SqliteErrorCode}: {se.Message}";
                    try
                    {
                        var details = new List<string>();
                        var fkCmd = conn.CreateCommand();
                        fkCmd.CommandText = "PRAGMA foreign_key_check;";
                        using var fkR = fkCmd.ExecuteReader();
                        while (fkR.Read())
                        {
                            var table = fkR.IsDBNull(0) ? "" : fkR.GetString(0);
                            var rowid = fkR.IsDBNull(1) ? "" : fkR.GetValue(1).ToString();
                            var parent = fkR.IsDBNull(2) ? "" : fkR.GetString(2);
                            details.Add($"Table={table}, RowId={rowid}, Parent={parent}");
                        }
                        if (details.Count > 0)
                        {
                            msg += $"; Foreign key check: {string.Join("; ", details)}";
                        }
                    }
                    catch { }

                    throw new Exception(msg, ex);
                }

                throw;
            }
        }

        public static string NormalizePaperBillNo(string pno)
        {
            if (string.IsNullOrWhiteSpace(pno)) return string.Empty;
            string trimmed = pno.Trim();
            
            // Replace leading zeros in numeric sequences (e.g. "002" -> "2", "PB-002" -> "PB-2", "0" -> "0")
            string normalized = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\b0+([1-9]\d*)", "$1");
            
            // If the entire string was only zeros (e.g. "000"), normalize to "0"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^0+$"))
                return "0";

            return normalized.ToLowerInvariant();
        }

        public static bool IsPaperBillNoExists(string paperBillNo, int excludeInvoiceId = 0)
        {
            if (string.IsNullOrWhiteSpace(paperBillNo)) return false;

            string targetNormalized = NormalizePaperBillNo(paperBillNo);
            if (string.IsNullOrEmpty(targetNormalized)) return false;

            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, PaperBillNo FROM Invoices WHERE PaperBillNo IS NOT NULL AND PaperBillNo <> '' AND Id <> $exId";
            cmd.Parameters.AddWithValue("$exId", excludeInvoiceId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string existingPno = r["PaperBillNo"]?.ToString();
                if (!string.IsNullOrWhiteSpace(existingPno))
                {
                    if (NormalizePaperBillNo(existingPno) == targetNormalized)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static List<Invoice> GetInvoices()
        {
            var list = new List<Invoice>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT i.*, c.Name as CustomerName FROM Invoices i
                LEFT JOIN Customers c ON i.CustomerId = c.Id ORDER BY i.Id DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Invoice
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    InvoiceNo = r["InvoiceNo"].ToString(),
                    CustomerId = r["CustomerId"] is DBNull ? 0 : Convert.ToInt32(r["CustomerId"]),
                    CustomerName = r["CustomerName"]?.ToString() ?? "Walk-in",
                    InvoiceDate = DateTime.Parse(r["InvoiceDate"].ToString()),
                    SubTotal = Convert.ToDecimal(r["SubTotal"]),
                    GstAmount = Convert.ToDecimal(r["GstAmount"]),
                    GrandTotal = Convert.ToDecimal(r["GrandTotal"])
                });
            }
            return list;
        }

        // ---- Product Sales History ----

        /// <summary>
        /// Returns customer-wise sales history for a specific product.
        /// Each row = one invoice line where this product was sold.
        /// </summary>
        public static List<ProductSaleRecord> GetProductSalesHistory(int productId)
        {
            var list = new List<ProductSaleRecord>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ii.Qty, ii.Rate, ii.Amount, ii.GstPercent,
                       i.InvoiceNo, i.InvoiceDate, i.Id as InvoiceId,
                       COALESCE(NULLIF(f.FarmerName, ''), NULLIF(i.CustomerName, ''), c.Name, 'Walk-in Farmer') as CustomerName,
                       COALESCE(NULLIF(f.MobileNumber, ''), NULLIF(i.MobileNumber, ''), c.Phone, '-') as CustomerPhone
                FROM InvoiceItems ii
                INNER JOIN Invoices i ON ii.InvoiceId = i.Id
                LEFT JOIN Farmers f ON i.FarmerId = f.FarmerId
                LEFT JOIN Customers c ON i.CustomerId = c.Id
                WHERE ii.ProductId = $pid
                ORDER BY i.InvoiceDate DESC";
            cmd.Parameters.AddWithValue("$pid", productId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ProductSaleRecord
                {
                    InvoiceId = Convert.ToInt32(r["InvoiceId"]),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    InvoiceDate = DateTime.Parse(r["InvoiceDate"].ToString()),
                    CustomerName = r["CustomerName"]?.ToString(),
                    CustomerPhone = r["CustomerPhone"]?.ToString(),
                    Qty = Convert.ToInt32(r["Qty"]),
                    Rate = Convert.ToDecimal(r["Rate"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    Amount = Convert.ToDecimal(r["Amount"])
                });
            }
            return list;
        }

        /// <summary>
        /// Returns complete purchase and return history for a specific product.
        /// </summary>
        public static List<ProductPurchaseRecord> GetProductPurchaseHistory(int productId)
        {
            var list = new List<ProductPurchaseRecord>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 'Purchase Entry' as TransactionType, p.PurchaseId, p.PurchaseNumber, p.PurchaseDate,
                       COALESCE(s.Name, '-') as SupplierName, pi.Quantity, pi.FreeQuantity,
                       pi.PurchasePrice, pi.Amount, pi.BatchNumber, pi.ExpiryDate
                FROM PurchaseItems pi
                INNER JOIN Purchases p ON pi.PurchaseId = p.PurchaseId
                LEFT JOIN Suppliers s ON p.SupplierId = s.Id
                WHERE pi.ProductId = $pid

                UNION ALL

                SELECT 'Purchase Return' as TransactionType, pr.PurchaseReturnId as PurchaseId, pr.ReturnNumber as PurchaseNumber, pr.ReturnDate as PurchaseDate,
                       COALESCE(s.Name, '-') as SupplierName, pri.ReturnQuantity as Quantity, 0 as FreeQuantity,
                       pri.PurchasePrice, pri.Amount, pri.BatchNumber, pri.ExpiryDate
                FROM PurchaseReturnItems pri
                INNER JOIN PurchaseReturns pr ON pri.PurchaseReturnId = pr.PurchaseReturnId
                LEFT JOIN Purchases p ON pr.PurchaseId = p.PurchaseId
                LEFT JOIN Suppliers s ON pr.SupplierId = s.Id OR p.SupplierId = s.Id
                WHERE pri.ProductId = $pid

                ORDER BY PurchaseDate DESC";
            cmd.Parameters.AddWithValue("$pid", productId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ProductPurchaseRecord
                {
                    TransactionType = r["TransactionType"]?.ToString() ?? "Purchase Entry",
                    PurchaseId = r["PurchaseId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseId"]),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    PurchaseDate = string.IsNullOrWhiteSpace(r["PurchaseDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["PurchaseDate"].ToString()),
                    SupplierName = r["SupplierName"]?.ToString(),
                    Quantity = r["Quantity"] is DBNull ? 0 : Convert.ToInt32(r["Quantity"]),
                    FreeQuantity = r["FreeQuantity"] is DBNull ? 0 : Convert.ToInt32(r["FreeQuantity"]),
                    PurchasePrice = r["PurchasePrice"] is DBNull ? 0 : Convert.ToDecimal(r["PurchasePrice"]),
                    Amount = r["Amount"] is DBNull ? 0 : Convert.ToDecimal(r["Amount"]),
                    BatchNumber = r["BatchNumber"]?.ToString(),
                    ExpiryDate = r["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString())
                });
            }
            return list;
        }

        // ---------------- Dashboard summary ----------------

        public static int GetTotalProducts()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Products";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static int GetTotalCustomers()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Customers";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static decimal GetTodaysSales()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(SUM(GrandTotal),0) FROM Invoices WHERE date(InvoiceDate) = date('now','localtime')";
            var result = cmd.ExecuteScalar();
            return result is DBNull ? 0 : Convert.ToDecimal(result);
        }

        public static int GetLowStockCount()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE StockQty <= ReorderLevel";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static int GetActiveProductsCount()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Products WHERE Status=1";
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static List<string> GetDistinctCompanies()
        {
            var list = new List<string>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Company FROM Products WHERE Company IS NOT NULL AND Company<>'' ORDER BY Company";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r[0]?.ToString());
            return list;
        }

        public static List<Product> GetLowStockProducts()
        {
            var list = new List<Product>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT p.*, c.Name as CategoryName FROM Products p
                LEFT JOIN Categories c ON p.CategoryId = c.Id
                WHERE p.StockQty <= p.ReorderLevel ORDER BY p.StockQty ASC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Product
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    Name = r["Name"].ToString(),
                    CategoryName = r["CategoryName"]?.ToString(),
                    StockQty = Convert.ToInt32(r["StockQty"]),
                    ReorderLevel = Convert.ToInt32(r["ReorderLevel"]),
                    Unit = r["Unit"]?.ToString()
                });
            }
            return list;
        }

        // ---------------- Purchase Returns ----------------

        public static string GenerateNextPurchaseReturnNo()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(PurchaseReturnId), 0) FROM PurchaseReturns";
            long maxId = Convert.ToInt64(cmd.ExecuteScalar());
            return $"PR-{DateTime.Now:yyyy}-{(maxId + 1):00000}";
        }

        public static int GetAlreadyReturnedQty(int purchaseItemId, SqliteConnection conn = null)
        {
            var connection = conn ?? GetConnection();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT IFNULL(SUM(ReturnQuantity), 0) FROM PurchaseReturnItems WHERE PurchaseItemId=$pid";
                cmd.Parameters.AddWithValue("$pid", purchaseItemId);
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
            finally
            {
                if (conn == null) connection.Dispose();
            }
        }

        public static List<Purchase> GetPurchasesForReturnSelection(string search = null)
        {
            var list = new List<Purchase>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(p.PurchaseNumber LIKE $s OR s.Name LIKE $s OR p.SupplierInvoiceNumber LIKE $s OR p.PaperBillNumber LIKE $s)";

            var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT p.*, s.Name as SupplierName FROM Purchases p LEFT JOIN Suppliers s ON p.SupplierId=s.Id WHERE {where} ORDER BY p.PurchaseId DESC LIMIT 50";
            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Purchase
                {
                    PurchaseId = r.GetInt32(r.GetOrdinal("PurchaseId")),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    SupplierId = r["SupplierId"] is DBNull ? 0 : Convert.ToInt32(r["SupplierId"]),
                    SupplierName = r["SupplierName"]?.ToString(),
                    SupplierInvoiceNumber = r["SupplierInvoiceNumber"]?.ToString(),
                    PaperBillNumber = r["PaperBillNumber"]?.ToString(),
                    PurchaseDate = string.IsNullOrWhiteSpace(r["PurchaseDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["PurchaseDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    PaidAmount = r["PaidAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PaidAmount"]),
                    PayableAmount = r["PayableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["PayableAmount"])
                });
            }
            return list;
        }

        public static int SavePurchaseReturn(PurchaseReturn returnHeader, List<PurchaseReturnItem> items)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // Re-validate all items against live DB state to prevent over-returns & negative stock
                foreach (var item in items)
                {
                    // 1. Get original purchase item quantity
                    var chkItemCmd = conn.CreateCommand();
                    chkItemCmd.CommandText = "SELECT Quantity, ProductId, ProductName FROM PurchaseItems WHERE PurchaseItemId=$piid";
                    chkItemCmd.Parameters.AddWithValue("$piid", item.PurchaseItemId);
                    using var r = chkItemCmd.ExecuteReader();
                    if (!r.Read())
                    {
                        throw new Exception($"Purchase item #{item.PurchaseItemId} not found.");
                    }
                    int purchasedQty = Convert.ToInt32(r["Quantity"]);
                    int productId = Convert.ToInt32(r["ProductId"]);
                    string prodName = r["ProductName"]?.ToString() ?? item.ProductName;
                    r.Close();

                    // 2. Re-query already returned quantity
                    int alreadyReturned = GetAlreadyReturnedQty(item.PurchaseItemId, conn);
                    int actualReturnable = purchasedQty - alreadyReturned;

                    if (item.ReturnQuantity > actualReturnable)
                    {
                        throw new Exception($"Return quantity for {prodName} ({item.ReturnQuantity}) exceeds the remaining returnable quantity ({actualReturnable}).");
                    }

                    // 3. Re-query batch stock quantity
                    var stockCmd = conn.CreateCommand();
                    stockCmd.CommandText = "SELECT StockQty FROM Products WHERE Id=$prid";
                    stockCmd.Parameters.AddWithValue("$prid", productId);
                    var currStockObj = stockCmd.ExecuteScalar();
                    int currStock = currStockObj != null && currStockObj != DBNull.Value ? Convert.ToInt32(currStockObj) : 0;

                    if (item.ReturnQuantity > currStock)
                    {
                        throw new Exception($"Return quantity for {prodName} ({item.ReturnQuantity}) exceeds available stock ({currStock}).");
                    }
                }

                // Insert Purchase Return Header
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO PurchaseReturns (ReturnNumber, PurchaseId, SupplierId, SupplierInvoiceNumber, PaperBillNumber, ReturnDate, SubTotal, Discount, TaxableAmount, GSTAmount, RoundOff, GrandTotal, ReturnReason, Notes, Status, CreatedAt)
                    VALUES ($rno,$pid,$sid,$sino,$pno,$rdt,$sub,$disc,$tax,$gst,$ro,$grand,$rr,$notes,$st,$ca);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$rno", returnHeader.ReturnNumber);
                cmd.Parameters.AddWithValue("$pid", returnHeader.PurchaseId);
                cmd.Parameters.AddWithValue("$sid", returnHeader.SupplierId);
                cmd.Parameters.AddWithValue("$sino", returnHeader.SupplierInvoiceNumber ?? "");
                cmd.Parameters.AddWithValue("$pno", returnHeader.PaperBillNumber ?? "");
                cmd.Parameters.AddWithValue("$rdt", returnHeader.ReturnDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$sub", returnHeader.SubTotal);
                cmd.Parameters.AddWithValue("$disc", returnHeader.Discount);
                cmd.Parameters.AddWithValue("$tax", returnHeader.TaxableAmount);
                cmd.Parameters.AddWithValue("$gst", returnHeader.GSTAmount);
                cmd.Parameters.AddWithValue("$ro", returnHeader.RoundOff);
                cmd.Parameters.AddWithValue("$grand", returnHeader.GrandTotal);
                cmd.Parameters.AddWithValue("$rr", returnHeader.ReturnReason ?? "");
                cmd.Parameters.AddWithValue("$notes", returnHeader.Notes ?? "");
                cmd.Parameters.AddWithValue("$st", string.IsNullOrWhiteSpace(returnHeader.Status) ? "Completed" : returnHeader.Status);
                cmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                int returnId = Convert.ToInt32((long)cmd.ExecuteScalar());

                // Insert Return Items & Reduce Product Stock
                foreach (var item in items)
                {
                    int prodId = item.ProductId;

                    // If ProductId is not set, resolve from PurchaseItems or Products table
                    if (prodId <= 0 && item.PurchaseItemId > 0)
                    {
                        var checkPiCmd = conn.CreateCommand();
                        checkPiCmd.CommandText = "SELECT ProductId FROM PurchaseItems WHERE PurchaseItemId = $piid";
                        checkPiCmd.Parameters.AddWithValue("$piid", item.PurchaseItemId);
                        var res = checkPiCmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value) prodId = Convert.ToInt32(res);
                    }

                    if (prodId <= 0 && !string.IsNullOrWhiteSpace(item.ProductName))
                    {
                        var checkPrCmd = conn.CreateCommand();
                        checkPrCmd.CommandText = "SELECT Id FROM Products WHERE LOWER(TRIM(Name)) = LOWER(TRIM($n)) LIMIT 1";
                        checkPrCmd.Parameters.AddWithValue("$n", item.ProductName);
                        var res = checkPrCmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value) prodId = Convert.ToInt32(res);
                    }

                    var icmd = conn.CreateCommand();
                    icmd.CommandText = @"INSERT INTO PurchaseReturnItems (PurchaseReturnId, PurchaseItemId, ProductId, ProductName, Company, BatchNumber, ExpiryDate, PurchasedQuantity, AlreadyReturnedQuantity, ReturnableQuantity, ReturnQuantity, PurchasePrice, GST, Amount)
                        VALUES ($prid,$piid,$pid,$pn,$co,$bn,$ed,$pq,$arq,$req,$rqty,$pp,$gst,$a);";
                    icmd.Parameters.AddWithValue("$prid", returnId);
                    icmd.Parameters.AddWithValue("$piid", item.PurchaseItemId);
                    icmd.Parameters.AddWithValue("$pid", prodId > 0 ? (object)prodId : DBNull.Value);
                    icmd.Parameters.AddWithValue("$pn", item.ProductName ?? "");
                    icmd.Parameters.AddWithValue("$co", item.Company ?? "");
                    icmd.Parameters.AddWithValue("$bn", item.BatchNumber ?? "");
                    icmd.Parameters.AddWithValue("$ed", item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                    icmd.Parameters.AddWithValue("$pq", item.PurchasedQuantity);
                    icmd.Parameters.AddWithValue("$arq", item.AlreadyReturnedQuantity);
                    icmd.Parameters.AddWithValue("$req", item.ReturnableQuantity);
                    icmd.Parameters.AddWithValue("$rqty", item.ReturnQuantity);
                    icmd.Parameters.AddWithValue("$pp", item.PurchasePrice);
                    icmd.Parameters.AddWithValue("$gst", item.GST);
                    icmd.Parameters.AddWithValue("$a", item.Amount);
                    icmd.ExecuteNonQuery();

                    // Reduce Stock (Negative delta)
                    if (prodId > 0)
                    {
                        AdjustStock(prodId, -item.ReturnQuantity, conn);
                    }
                }

                // Adjust Supplier Payable balance if purchase has unpaid amount
                if (returnHeader.PurchaseId > 0 && returnHeader.GrandTotal > 0)
                {
                    var adjCmd = conn.CreateCommand();
                    adjCmd.CommandText = "UPDATE Purchases SET PayableAmount = MAX(0, PayableAmount - $ret) WHERE PurchaseId = $pid";
                    adjCmd.Parameters.AddWithValue("$ret", returnHeader.GrandTotal);
                    adjCmd.Parameters.AddWithValue("$pid", returnHeader.PurchaseId);
                    adjCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return returnId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                if (ex is SqliteException se)
                {
                    var msg = $"SQLite Error {se.SqliteErrorCode}: {se.Message}";
                    try
                    {
                        var details = new List<string>();
                        var fkCmd = conn.CreateCommand();
                        fkCmd.CommandText = "PRAGMA foreign_key_check;";
                        using var fkR = fkCmd.ExecuteReader();
                        while (fkR.Read())
                        {
                            var table = fkR.IsDBNull(0) ? "" : fkR.GetString(0);
                            var rowid = fkR.IsDBNull(1) ? "" : fkR.GetValue(1).ToString();
                            var parent = fkR.IsDBNull(2) ? "" : fkR.GetString(2);
                            details.Add($"Table={table}, RowId={rowid}, Parent={parent}");
                        }
                        if (details.Count > 0)
                        {
                            msg += $"; Foreign key check: {string.Join("; ", details)}";
                        }
                    }
                    catch { }

                    throw new Exception(msg, ex);
                }

                throw;
            }
        }

        public static (List<PurchaseReturn> Items, int Total) GetPurchaseReturnsPaged(string search = null, int page = 1, int pageSize = 20)
        {
            var list = new List<PurchaseReturn>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(pr.ReturnNumber LIKE $s OR p.PurchaseNumber LIKE $s OR s.Name LIKE $s OR pr.SupplierInvoiceNumber LIKE $s)";

            var cnt = conn.CreateCommand();
            cnt.CommandText = $"SELECT COUNT(*) FROM PurchaseReturns pr LEFT JOIN Purchases p ON pr.PurchaseId=p.PurchaseId LEFT JOIN Suppliers s ON pr.SupplierId=s.Id WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cnt.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            var total = Convert.ToInt32((long)cnt.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var data = conn.CreateCommand();
            data.CommandText = $"SELECT pr.*, p.PurchaseNumber, s.Name as SupplierName FROM PurchaseReturns pr LEFT JOIN Purchases p ON pr.PurchaseId=p.PurchaseId LEFT JOIN Suppliers s ON pr.SupplierId=s.Id WHERE {where} ORDER BY pr.PurchaseReturnId DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) data.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            data.Parameters.AddWithValue("$limit", pageSize);
            data.Parameters.AddWithValue("$offset", offset);
            using var r = data.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PurchaseReturn
                {
                    PurchaseReturnId = r.GetInt32(r.GetOrdinal("PurchaseReturnId")),
                    ReturnNumber = r["ReturnNumber"]?.ToString(),
                    PurchaseId = r["PurchaseId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseId"]),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    SupplierId = r["SupplierId"] is DBNull ? 0 : Convert.ToInt32(r["SupplierId"]),
                    SupplierName = r["SupplierName"]?.ToString(),
                    SupplierInvoiceNumber = r["SupplierInvoiceNumber"]?.ToString(),
                    PaperBillNumber = r["PaperBillNumber"]?.ToString(),
                    ReturnDate = string.IsNullOrWhiteSpace(r["ReturnDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["ReturnDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    RoundOff = r["RoundOff"] is DBNull ? 0 : Convert.ToDecimal(r["RoundOff"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    ReturnReason = r["ReturnReason"]?.ToString(),
                    Notes = r["Notes"]?.ToString(),
                    Status = r["Status"]?.ToString() ?? "Completed",
                    CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
                });
            }
            return (list, total);
        }

        public static PurchaseReturn GetPurchaseReturnById(int returnId)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT pr.*, p.PurchaseNumber, s.Name as SupplierName FROM PurchaseReturns pr LEFT JOIN Purchases p ON pr.PurchaseId=p.PurchaseId LEFT JOIN Suppliers s ON pr.SupplierId=s.Id WHERE pr.PurchaseReturnId=$id";
            cmd.Parameters.AddWithValue("$id", returnId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new PurchaseReturn
                {
                    PurchaseReturnId = r.GetInt32(r.GetOrdinal("PurchaseReturnId")),
                    ReturnNumber = r["ReturnNumber"]?.ToString(),
                    PurchaseId = r["PurchaseId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseId"]),
                    PurchaseNumber = r["PurchaseNumber"]?.ToString(),
                    SupplierId = r["SupplierId"] is DBNull ? 0 : Convert.ToInt32(r["SupplierId"]),
                    SupplierName = r["SupplierName"]?.ToString(),
                    SupplierInvoiceNumber = r["SupplierInvoiceNumber"]?.ToString(),
                    PaperBillNumber = r["PaperBillNumber"]?.ToString(),
                    ReturnDate = string.IsNullOrWhiteSpace(r["ReturnDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["ReturnDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    RoundOff = r["RoundOff"] is DBNull ? 0 : Convert.ToDecimal(r["RoundOff"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    ReturnReason = r["ReturnReason"]?.ToString(),
                    Notes = r["Notes"]?.ToString(),
                    Status = r["Status"]?.ToString() ?? "Completed",
                    CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
                };
            }
            return null;
        }

        public static List<PurchaseReturnItem> GetPurchaseReturnItems(int returnId)
        {
            var list = new List<PurchaseReturnItem>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PurchaseReturnItems WHERE PurchaseReturnId=$id";
            cmd.Parameters.AddWithValue("$id", returnId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PurchaseReturnItem
                {
                    PurchaseReturnItemId = r.GetInt32(r.GetOrdinal("PurchaseReturnItemId")),
                    PurchaseReturnId = r["PurchaseReturnId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseReturnId"]),
                    PurchaseItemId = r["PurchaseItemId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseItemId"]),
                    ProductId = r["ProductId"] is DBNull ? 0 : Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    BatchNumber = r["BatchNumber"]?.ToString(),
                    ExpiryDate = string.IsNullOrWhiteSpace(r["ExpiryDate"]?.ToString()) ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    PurchasedQuantity = r["PurchasedQuantity"] is DBNull ? 0 : Convert.ToInt32(r["PurchasedQuantity"]),
                    AlreadyReturnedQuantity = r["AlreadyReturnedQuantity"] is DBNull ? 0 : Convert.ToInt32(r["AlreadyReturnedQuantity"]),
                    ReturnableQuantity = r["ReturnableQuantity"] is DBNull ? 0 : Convert.ToInt32(r["ReturnableQuantity"]),
                    ReturnQuantity = r["ReturnQuantity"] is DBNull ? 0 : Convert.ToInt32(r["ReturnQuantity"]),
                    PurchasePrice = r["PurchasePrice"] is DBNull ? 0 : Convert.ToDecimal(r["PurchasePrice"]),
                    GST = r["GST"] is DBNull ? 0 : Convert.ToDecimal(r["GST"]),
                    Amount = r["Amount"] is DBNull ? 0 : Convert.ToDecimal(r["Amount"])
                });
            }
            return list;
        }

        // ===================== Sales Return Module =====================

        public static string GenerateNextSalesReturnNo()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM SalesReturns";
            var count = Convert.ToInt32((long)cmd.ExecuteScalar()) + 1;
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            return $"SR-{dateStr}-{count:D4}";
        }

        public static int GetAlreadyReturnedSalesQty(int invoiceItemId)
        {
            if (invoiceItemId <= 0) return 0;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(SUM(ReturnQuantity), 0) FROM SalesReturnItems WHERE InvoiceItemId = $iiid";
            cmd.Parameters.AddWithValue("$iiid", invoiceItemId);
            return Convert.ToInt32((long)cmd.ExecuteScalar());
        }

        public static int SaveSalesReturn(SalesReturn returnHeader, List<SalesReturnItem> items)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO SalesReturns (ReturnNumber, InvoiceId, InvoiceNo, FarmerId, FarmerName, MobileNumber, VillageName, ReturnDate, SubTotal, Discount, TaxableAmount, GSTAmount, RoundOff, GrandTotal, AdjustmentType, ReturnReason, Notes, Status, CreatedAt)
                    VALUES ($rno,$iid,$ino,$fid,$fname,$mob,$vil,$rdt,$sub,$disc,$tax,$gst,$ro,$grand,$adj,$rr,$notes,$st,$ca);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$rno", returnHeader.ReturnNumber);
                cmd.Parameters.AddWithValue("$iid", returnHeader.InvoiceId);
                cmd.Parameters.AddWithValue("$ino", returnHeader.InvoiceNo ?? "");
                cmd.Parameters.AddWithValue("$fid", returnHeader.FarmerId);
                cmd.Parameters.AddWithValue("$fname", returnHeader.FarmerName ?? "");
                cmd.Parameters.AddWithValue("$mob", returnHeader.MobileNumber ?? "");
                cmd.Parameters.AddWithValue("$vil", returnHeader.VillageName ?? "");
                cmd.Parameters.AddWithValue("$rdt", returnHeader.ReturnDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$sub", returnHeader.SubTotal);
                cmd.Parameters.AddWithValue("$disc", returnHeader.Discount);
                cmd.Parameters.AddWithValue("$tax", returnHeader.TaxableAmount);
                cmd.Parameters.AddWithValue("$gst", returnHeader.GSTAmount);
                cmd.Parameters.AddWithValue("$ro", returnHeader.RoundOff);
                cmd.Parameters.AddWithValue("$grand", returnHeader.GrandTotal);
                cmd.Parameters.AddWithValue("$adj", returnHeader.AdjustmentType ?? "Udhar Adjustment");
                cmd.Parameters.AddWithValue("$rr", returnHeader.ReturnReason ?? "");
                cmd.Parameters.AddWithValue("$notes", returnHeader.Notes ?? "");
                cmd.Parameters.AddWithValue("$st", string.IsNullOrWhiteSpace(returnHeader.Status) ? "Completed" : returnHeader.Status);
                cmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                int returnId = Convert.ToInt32((long)cmd.ExecuteScalar());

                // Insert Return Items, Increase Product Stock & Adjust Udhar Balance
                foreach (var item in items)
                {
                    int prodId = item.ProductId;
                    if (prodId <= 0 && item.InvoiceItemId > 0)
                    {
                        var checkIiCmd = conn.CreateCommand();
                        checkIiCmd.CommandText = "SELECT ProductId FROM InvoiceItems WHERE Id = $iiid";
                        checkIiCmd.Parameters.AddWithValue("$iiid", item.InvoiceItemId);
                        var res = checkIiCmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value) prodId = Convert.ToInt32(res);
                    }

                    if (prodId <= 0 && !string.IsNullOrWhiteSpace(item.ProductName))
                    {
                        var checkPrCmd = conn.CreateCommand();
                        checkPrCmd.CommandText = "SELECT Id FROM Products WHERE LOWER(TRIM(Name)) = LOWER(TRIM($n)) LIMIT 1";
                        checkPrCmd.Parameters.AddWithValue("$n", item.ProductName);
                        var res = checkPrCmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value) prodId = Convert.ToInt32(res);
                    }

                    var icmd = conn.CreateCommand();
                    icmd.CommandText = @"INSERT INTO SalesReturnItems (SalesReturnId, InvoiceItemId, ProductId, ProductName, Company, BatchNumber, ExpiryDate, PurchasedQuantity, AlreadyReturnedQuantity, ReturnableQuantity, ReturnQuantity, Rate, GstPercent, Amount)
                        VALUES ($srid,$iiid,$pid,$pn,$co,$bn,$ed,$pq,$arq,$req,$rqty,$rt,$g,$a);";
                    icmd.Parameters.AddWithValue("$srid", returnId);
                    icmd.Parameters.AddWithValue("$iiid", item.InvoiceItemId);
                    icmd.Parameters.AddWithValue("$pid", prodId > 0 ? (object)prodId : DBNull.Value);
                    icmd.Parameters.AddWithValue("$pn", item.ProductName ?? "");
                    icmd.Parameters.AddWithValue("$co", item.Company ?? "");
                    icmd.Parameters.AddWithValue("$bn", item.BatchNumber ?? "");
                    icmd.Parameters.AddWithValue("$ed", item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                    icmd.Parameters.AddWithValue("$pq", item.PurchasedQuantity);
                    icmd.Parameters.AddWithValue("$arq", item.AlreadyReturnedQuantity);
                    icmd.Parameters.AddWithValue("$req", item.ReturnableQuantity);
                    icmd.Parameters.AddWithValue("$rqty", item.ReturnQuantity);
                    icmd.Parameters.AddWithValue("$rt", item.Rate);
                    icmd.Parameters.AddWithValue("$g", item.GstPercent);
                    icmd.Parameters.AddWithValue("$a", item.Amount);
                    icmd.ExecuteNonQuery();

                    // Restore Stock (+ Positive delta)
                    if (prodId > 0)
                    {
                        AdjustStock(prodId, +item.ReturnQuantity, conn);
                    }
                }

                // Adjust Invoice / Farmer Balance if returning unpaid bill amount or Udhar Adjustment
                if (returnHeader.InvoiceId > 0 && returnHeader.GrandTotal > 0)
                {
                    var adjCmd = conn.CreateCommand();
                    adjCmd.CommandText = "UPDATE Invoices SET PayableAmount = MAX(0, PayableAmount - $ret) WHERE Id = $iid";
                    adjCmd.Parameters.AddWithValue("$ret", returnHeader.GrandTotal);
                    adjCmd.Parameters.AddWithValue("$iid", returnHeader.InvoiceId);
                    adjCmd.ExecuteNonQuery();
                }

                tx.Commit();
                return returnId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static (List<SalesReturn> Items, int Total) GetSalesReturnsPaged(string search = null, int page = 1, int pageSize = 20)
        {
            var list = new List<SalesReturn>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(sr.ReturnNumber LIKE $s OR sr.InvoiceNo LIKE $s OR sr.FarmerName LIKE $s OR sr.MobileNumber LIKE $s OR sr.VillageName LIKE $s)";

            var cnt = conn.CreateCommand();
            cnt.CommandText = $"SELECT COUNT(*) FROM SalesReturns sr WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cnt.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            var total = Convert.ToInt32((long)cnt.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var data = conn.CreateCommand();
            data.CommandText = $"SELECT sr.* FROM SalesReturns sr WHERE {where} ORDER BY sr.SalesReturnId DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) data.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            data.Parameters.AddWithValue("$limit", pageSize);
            data.Parameters.AddWithValue("$offset", offset);

            using var r = data.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SalesReturn
                {
                    SalesReturnId = r.GetInt32(r.GetOrdinal("SalesReturnId")),
                    ReturnNumber = r["ReturnNumber"]?.ToString(),
                    InvoiceId = r["InvoiceId"] is DBNull ? 0 : Convert.ToInt32(r["InvoiceId"]),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    FarmerId = r["FarmerId"] is DBNull ? 0 : Convert.ToInt32(r["FarmerId"]),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    ReturnDate = string.IsNullOrWhiteSpace(r["ReturnDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["ReturnDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    RoundOff = r["RoundOff"] is DBNull ? 0 : Convert.ToDecimal(r["RoundOff"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    AdjustmentType = r["AdjustmentType"]?.ToString() ?? "Udhar Adjustment",
                    ReturnReason = r["ReturnReason"]?.ToString(),
                    Notes = r["Notes"]?.ToString(),
                    Status = r["Status"]?.ToString() ?? "Completed",
                    CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
                });
            }
            return (list, total);
        }

        public static SalesReturn GetSalesReturnById(int returnId)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM SalesReturns WHERE SalesReturnId=$id";
            cmd.Parameters.AddWithValue("$id", returnId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new SalesReturn
                {
                    SalesReturnId = r.GetInt32(r.GetOrdinal("SalesReturnId")),
                    ReturnNumber = r["ReturnNumber"]?.ToString(),
                    InvoiceId = r["InvoiceId"] is DBNull ? 0 : Convert.ToInt32(r["InvoiceId"]),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    FarmerId = r["FarmerId"] is DBNull ? 0 : Convert.ToInt32(r["FarmerId"]),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    ReturnDate = string.IsNullOrWhiteSpace(r["ReturnDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["ReturnDate"].ToString()),
                    SubTotal = r["SubTotal"] is DBNull ? 0 : Convert.ToDecimal(r["SubTotal"]),
                    Discount = r["Discount"] is DBNull ? 0 : Convert.ToDecimal(r["Discount"]),
                    TaxableAmount = r["TaxableAmount"] is DBNull ? 0 : Convert.ToDecimal(r["TaxableAmount"]),
                    GSTAmount = r["GSTAmount"] is DBNull ? 0 : Convert.ToDecimal(r["GSTAmount"]),
                    RoundOff = r["RoundOff"] is DBNull ? 0 : Convert.ToDecimal(r["RoundOff"]),
                    GrandTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    AdjustmentType = r["AdjustmentType"]?.ToString() ?? "Udhar Adjustment",
                    ReturnReason = r["ReturnReason"]?.ToString(),
                    Notes = r["Notes"]?.ToString(),
                    Status = r["Status"]?.ToString() ?? "Completed",
                    CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
                };
            }
            return null;
        }

        public static List<SalesReturnItem> GetSalesReturnItems(int returnId)
        {
            var list = new List<SalesReturnItem>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM SalesReturnItems WHERE SalesReturnId=$id";
            cmd.Parameters.AddWithValue("$id", returnId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SalesReturnItem
                {
                    SalesReturnItemId = r.GetInt32(r.GetOrdinal("SalesReturnItemId")),
                    SalesReturnId = r["SalesReturnId"] is DBNull ? 0 : Convert.ToInt32(r["SalesReturnId"]),
                    InvoiceItemId = r["InvoiceItemId"] is DBNull ? 0 : Convert.ToInt32(r["InvoiceItemId"]),
                    ProductId = r["ProductId"] is DBNull ? 0 : Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString(),
                    Company = r["Company"]?.ToString(),
                    BatchNumber = r["BatchNumber"]?.ToString(),
                    ExpiryDate = string.IsNullOrWhiteSpace(r["ExpiryDate"]?.ToString()) ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    PurchasedQuantity = r["PurchasedQuantity"] is DBNull ? 0 : Convert.ToInt32(r["PurchasedQuantity"]),
                    AlreadyReturnedQuantity = r["AlreadyReturnedQuantity"] is DBNull ? 0 : Convert.ToInt32(r["AlreadyReturnedQuantity"]),
                    ReturnableQuantity = r["ReturnableQuantity"] is DBNull ? 0 : Convert.ToInt32(r["ReturnableQuantity"]),
                    ReturnQuantity = r["ReturnQuantity"] is DBNull ? 0 : Convert.ToInt32(r["ReturnQuantity"]),
                    Rate = r["Rate"] is DBNull ? 0 : Convert.ToDecimal(r["Rate"]),
                    GstPercent = r["GstPercent"] is DBNull ? 0 : Convert.ToDecimal(r["GstPercent"]),
                    Amount = r["Amount"] is DBNull ? 0 : Convert.ToDecimal(r["Amount"])
                });
            }
            return list;
        }

        // ===================== Payment Receipts / Jama Pavti =====================

        public static string GenerateNextReceiptNo()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(PaymentReceiptId), 0) FROM PaymentReceipts";
            long maxId = Convert.ToInt64(cmd.ExecuteScalar());
            return $"REC-{DateTime.Now:yyyy}-{(maxId + 1):00000}";
        }

        /// <summary>
        /// Finds the CustomerId(s) that match a Farmer by name (case-insensitive).
        /// Used to link farmers to invoices for outstanding calculation.
        /// </summary>
        public static List<int> GetCustomerIdsForFarmer(int farmerId, SqliteConnection conn = null)
        {
            var connection = conn ?? GetConnection();
            try
            {
                // First get the farmer name
                var fcmd = connection.CreateCommand();
                fcmd.CommandText = "SELECT FarmerName FROM Farmers WHERE FarmerId=$id";
                fcmd.Parameters.AddWithValue("$id", farmerId);
                var farmerName = fcmd.ExecuteScalar()?.ToString();
                if (string.IsNullOrWhiteSpace(farmerName)) return new List<int>();

                // Find matching customers by name
                var ccmd = connection.CreateCommand();
                ccmd.CommandText = "SELECT Id FROM Customers WHERE LOWER(Name) = LOWER($n)";
                ccmd.Parameters.AddWithValue("$n", farmerName.Trim());
                var ids = new List<int>();
                using var r = ccmd.ExecuteReader();
                while (r.Read()) ids.Add(r.GetInt32(0));
                return ids;
            }
            finally
            {
                if (conn == null) connection.Dispose();
            }
        }

        /// <summary>
        /// Calculates the total outstanding (udhar) balance for a farmer.
        /// Outstanding = SUM(GrandTotal of Udhar invoices for matching customers) 
        ///             - SUM(AllocatedAmount from PaymentReceiptAllocations for those invoices)
        /// </summary>
        public static decimal GetFarmerOutstandingBalance(int farmerId, SqliteConnection conn = null)
        {
            var connection = conn ?? GetConnection();
            try
            {
                // Total initial unpaid balance (PayableAmount) across all active invoices for this farmer
                var totalCmd = connection.CreateCommand();
                totalCmd.CommandText = "SELECT IFNULL(SUM(PayableAmount), 0) FROM Invoices WHERE FarmerId=$fid AND (Status IS NULL OR Status='Active')";
                totalCmd.Parameters.AddWithValue("$fid", farmerId);
                decimal totalPayable = Convert.ToDecimal(totalCmd.ExecuteScalar());

                // Total allocated payments via Payment Receipts (Jama Pavti)
                var paidCmd = connection.CreateCommand();
                paidCmd.CommandText = @"SELECT IFNULL(SUM(pra.AllocatedAmount), 0)
                    FROM PaymentReceiptAllocations pra
                    INNER JOIN Invoices i ON pra.InvoiceId = i.Id
                    WHERE i.FarmerId=$fid AND (i.Status IS NULL OR i.Status='Active')";
                paidCmd.Parameters.AddWithValue("$fid", farmerId);
                decimal totalReceiptPaid = Convert.ToDecimal(paidCmd.ExecuteScalar());

                return Math.Max(0m, totalPayable - totalReceiptPaid);
            }
            finally
            {
                if (conn == null) connection.Dispose();
            }
        }

        /// <summary>
        /// Gets outstanding invoices for a farmer (Invoices with remaining unpaid balance).
        /// Returns invoice details with outstanding = PayableAmount - SUM(AllocatedAmount).
        /// </summary>
        public static List<PaymentReceiptAllocation> GetOutstandingInvoicesForFarmer(int farmerId, SqliteConnection conn = null)
        {
            var connection = conn ?? GetConnection();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT i.Id, i.InvoiceNo, i.InvoiceDate, i.GrandTotal, i.PayableAmount,
                    IFNULL((SELECT SUM(pra.AllocatedAmount) FROM PaymentReceiptAllocations pra WHERE pra.InvoiceId = i.Id), 0) as TotalReceiptPaid
                    FROM Invoices i
                    WHERE i.FarmerId=$fid AND (i.Status IS NULL OR i.Status='Active') AND i.PayableAmount > 0
                    ORDER BY i.InvoiceDate ASC, i.Id ASC";
                cmd.Parameters.AddWithValue("$fid", farmerId);

                var list = new List<PaymentReceiptAllocation>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    decimal grandTotal = Convert.ToDecimal(r["GrandTotal"]);
                    decimal initialPayable = Convert.ToDecimal(r["PayableAmount"]);
                    decimal receiptPaid = Convert.ToDecimal(r["TotalReceiptPaid"]);
                    decimal outstanding = Math.Max(0m, initialPayable - receiptPaid);

                    if (outstanding <= 0) continue; // Fully settled, skip

                    list.Add(new PaymentReceiptAllocation
                    {
                        InvoiceId = r.GetInt32(r.GetOrdinal("Id")),
                        InvoiceNo = r["InvoiceNo"]?.ToString(),
                        InvoiceDate = DateTime.Parse(r["InvoiceDate"].ToString()),
                        InvoiceTotal = grandTotal,
                        InvoiceOutstanding = outstanding,
                        AllocatedAmount = 0m
                    });
                }
                return list;
            }
            finally
            {
                if (conn == null) connection.Dispose();
            }
        }

        /// <summary>
        /// Atomically saves a PaymentReceipt, its allocations, and validates all financial constraints.
        /// </summary>
        public static int SavePaymentReceipt(PaymentReceipt receipt, List<PaymentReceiptAllocation> allocations)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Re-validate farmer exists
                var fchk = conn.CreateCommand();
                fchk.CommandText = "SELECT COUNT(*) FROM Farmers WHERE FarmerId=$id";
                fchk.Parameters.AddWithValue("$id", receipt.FarmerId);
                if (Convert.ToInt32((long)fchk.ExecuteScalar()) == 0)
                    throw new Exception("Selected customer/farmer not found.");

                // 2. Recalculate live outstanding balance
                decimal liveBalance = GetFarmerOutstandingBalance(receipt.FarmerId, conn);
                receipt.OpeningBalance = liveBalance;

                if (receipt.ReceivedAmount <= 0)
                    throw new Exception("Received amount must be greater than zero.");

                if (receipt.ReceivedAmount > liveBalance)
                    throw new Exception($"Received amount (₹{receipt.ReceivedAmount:N2}) cannot exceed the current outstanding balance (₹{liveBalance:N2}).");

                // 3. Validate total allocation == received amount
                decimal totalAllocated = 0m;
                foreach (var a in allocations)
                    totalAllocated += a.AllocatedAmount;

                if (Math.Abs(totalAllocated - receipt.ReceivedAmount) > 0.01m)
                    throw new Exception($"Payment allocation (₹{totalAllocated:N2}) must equal the received amount (₹{receipt.ReceivedAmount:N2}).");

                // 4. Validate each allocation against live invoice outstanding
                foreach (var alloc in allocations)
                {
                    var ichk = conn.CreateCommand();
                    ichk.CommandText = @"SELECT i.GrandTotal,
                        IFNULL((SELECT SUM(pra.AllocatedAmount) FROM PaymentReceiptAllocations pra WHERE pra.InvoiceId = i.Id), 0) as TotalPaid
                        FROM Invoices i WHERE i.Id=$iid";
                    ichk.Parameters.AddWithValue("$iid", alloc.InvoiceId);
                    using var ir = ichk.ExecuteReader();
                    if (!ir.Read())
                        throw new Exception($"Invoice #{alloc.InvoiceId} not found.");

                    decimal invoiceTotal = Convert.ToDecimal(ir["GrandTotal"]);
                    decimal invoicePaid = Convert.ToDecimal(ir["TotalPaid"]);
                    decimal invoiceOutstanding = invoiceTotal - invoicePaid;
                    ir.Close();

                    if (alloc.AllocatedAmount > invoiceOutstanding + 0.01m)
                        throw new Exception($"Allocation for {alloc.InvoiceNo} (₹{alloc.AllocatedAmount:N2}) cannot exceed the outstanding amount (₹{invoiceOutstanding:N2}).");
                }

                // 5. Calculate closing balance
                receipt.ClosingBalance = liveBalance - receipt.ReceivedAmount;

                // 6. Insert PaymentReceipt header
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO PaymentReceipts (ReceiptNumber, FarmerId, FarmerName, MobileNumber, VillageName, ReceiptDate, OpeningBalance, ReceivedAmount, ClosingBalance, PaymentMode, TransactionReference, ChequeNumber, ChequeDate, BankName, Notes, CreatedAt)
                    VALUES ($rno,$fid,$fn,$mob,$vn,$rd,$ob,$ra,$cb,$pm,$tr,$cn,$cd,$bn,$notes,$ca);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$rno", receipt.ReceiptNumber);
                cmd.Parameters.AddWithValue("$fid", receipt.FarmerId);
                cmd.Parameters.AddWithValue("$fn", receipt.FarmerName ?? "");
                cmd.Parameters.AddWithValue("$mob", receipt.MobileNumber ?? "");
                cmd.Parameters.AddWithValue("$vn", receipt.VillageName ?? "");
                cmd.Parameters.AddWithValue("$rd", receipt.ReceiptDate.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$ob", receipt.OpeningBalance);
                cmd.Parameters.AddWithValue("$ra", receipt.ReceivedAmount);
                cmd.Parameters.AddWithValue("$cb", receipt.ClosingBalance);
                cmd.Parameters.AddWithValue("$pm", receipt.PaymentMode ?? "Cash");
                cmd.Parameters.AddWithValue("$tr", receipt.TransactionReference ?? "");
                cmd.Parameters.AddWithValue("$cn", receipt.ChequeNumber ?? "");
                cmd.Parameters.AddWithValue("$cd", receipt.ChequeDate ?? "");
                cmd.Parameters.AddWithValue("$bn", receipt.BankName ?? "");
                cmd.Parameters.AddWithValue("$notes", receipt.Notes ?? "");
                cmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                int receiptId = Convert.ToInt32((long)cmd.ExecuteScalar());

                // 7. Insert allocations
                foreach (var alloc in allocations)
                {
                    var acmd = conn.CreateCommand();
                    acmd.CommandText = @"INSERT INTO PaymentReceiptAllocations (PaymentReceiptId, InvoiceId, AllocatedAmount, CreatedAt)
                        VALUES ($prid,$iid,$aa,$ca);";
                    acmd.Parameters.AddWithValue("$prid", receiptId);
                    acmd.Parameters.AddWithValue("$iid", alloc.InvoiceId);
                    acmd.Parameters.AddWithValue("$aa", alloc.AllocatedAmount);
                    acmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    acmd.ExecuteNonQuery();
                }

                tx.Commit();
                return receiptId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static (List<PaymentReceipt> Items, int Total) GetPaymentReceiptsPaged(string search = null, int page = 1, int pageSize = 20)
        {
            var list = new List<PaymentReceipt>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(pr.ReceiptNumber LIKE $s OR pr.FarmerName LIKE $s OR pr.MobileNumber LIKE $s)";

            var cnt = conn.CreateCommand();
            cnt.CommandText = $"SELECT COUNT(*) FROM PaymentReceipts pr WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cnt.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            var total = Convert.ToInt32((long)cnt.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var data = conn.CreateCommand();
            data.CommandText = $"SELECT * FROM PaymentReceipts pr WHERE {where} ORDER BY pr.PaymentReceiptId DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) data.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            data.Parameters.AddWithValue("$limit", pageSize);
            data.Parameters.AddWithValue("$offset", offset);
            using var r = data.ExecuteReader();
            while (r.Read())
            {
                list.Add(ReadPaymentReceipt(r));
            }
            return (list, total);
        }

        public static PaymentReceipt GetPaymentReceiptById(int receiptId)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM PaymentReceipts WHERE PaymentReceiptId=$id";
            cmd.Parameters.AddWithValue("$id", receiptId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadPaymentReceipt(r) : null;
        }

        public static List<PaymentReceiptAllocation> GetPaymentReceiptAllocations(int receiptId)
        {
            var list = new List<PaymentReceiptAllocation>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT pra.*, i.InvoiceNo, i.InvoiceDate, i.GrandTotal
                FROM PaymentReceiptAllocations pra
                LEFT JOIN Invoices i ON pra.InvoiceId = i.Id
                WHERE pra.PaymentReceiptId=$id";
            cmd.Parameters.AddWithValue("$id", receiptId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PaymentReceiptAllocation
                {
                    PaymentReceiptAllocationId = r.GetInt32(r.GetOrdinal("PaymentReceiptAllocationId")),
                    PaymentReceiptId = Convert.ToInt32(r["PaymentReceiptId"]),
                    InvoiceId = Convert.ToInt32(r["InvoiceId"]),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    InvoiceDate = string.IsNullOrWhiteSpace(r["InvoiceDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["InvoiceDate"].ToString()),
                    InvoiceTotal = r["GrandTotal"] is DBNull ? 0 : Convert.ToDecimal(r["GrandTotal"]),
                    AllocatedAmount = r["AllocatedAmount"] is DBNull ? 0 : Convert.ToDecimal(r["AllocatedAmount"]),
                    CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
                });
            }
            return list;
        }

        public static List<Farmer> SearchFarmersByNameOrMobile(string search)
        {
            var list = new List<Farmer>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Farmers WHERE Status=1 AND (FarmerName LIKE $s OR MobileNumber LIKE $s) ORDER BY FarmerName LIMIT 50";
            cmd.Parameters.AddWithValue("$s", $"%{(search ?? "").Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Farmer
                {
                    FarmerId = r.GetInt32(r.GetOrdinal("FarmerId")),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                });
            }
            return list;
        }

        public static List<Farmer> SearchFarmersForPayment(string search)
        {
            var list = new List<Farmer>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Farmers WHERE Status=1 AND (FarmerName LIKE $s OR MobileNumber LIKE $s OR VillageName LIKE $s) ORDER BY FarmerName LIMIT 50";
            cmd.Parameters.AddWithValue("$s", $"%{(search ?? "").Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Farmer
                {
                    FarmerId = r.GetInt32(r.GetOrdinal("FarmerId")),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString(),
                    Status = r["Status"] is DBNull ? 1 : Convert.ToInt32(r["Status"])
                });
            }
            return list;
        }

        private static PaymentReceipt ReadPaymentReceipt(SqliteDataReader r)
        {
            return new PaymentReceipt
            {
                PaymentReceiptId = r.GetInt32(r.GetOrdinal("PaymentReceiptId")),
                ReceiptNumber = r["ReceiptNumber"]?.ToString(),
                FarmerId = r["FarmerId"] is DBNull ? 0 : Convert.ToInt32(r["FarmerId"]),
                FarmerName = r["FarmerName"]?.ToString(),
                MobileNumber = r["MobileNumber"]?.ToString(),
                VillageName = r["VillageName"]?.ToString(),
                ReceiptDate = string.IsNullOrWhiteSpace(r["ReceiptDate"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["ReceiptDate"].ToString()),
                OpeningBalance = r["OpeningBalance"] is DBNull ? 0 : Convert.ToDecimal(r["OpeningBalance"]),
                ReceivedAmount = r["ReceivedAmount"] is DBNull ? 0 : Convert.ToDecimal(r["ReceivedAmount"]),
                ClosingBalance = r["ClosingBalance"] is DBNull ? 0 : Convert.ToDecimal(r["ClosingBalance"]),
                PaymentMode = r["PaymentMode"]?.ToString(),
                TransactionReference = r["TransactionReference"]?.ToString(),
                ChequeNumber = r["ChequeNumber"]?.ToString(),
                ChequeDate = r["ChequeDate"]?.ToString(),
                BankName = r["BankName"]?.ToString(),
                Notes = r["Notes"]?.ToString(),
                CreatedAt = string.IsNullOrWhiteSpace(r["CreatedAt"]?.ToString()) ? DateTime.MinValue : DateTime.Parse(r["CreatedAt"].ToString())
            };
        }

        // ===================== Company Settings =====================

        public static CompanySettings GetCompanySettings()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM CompanySettings LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new CompanySettings
                {
                    Id = Convert.ToInt32(r["Id"]),
                    ShopName = r["ShopName"]?.ToString() ?? "Krushi Kendra Agriculture",
                    ShopAddress = r["ShopAddress"]?.ToString() ?? "",
                    ShopPhone = r["ShopPhone"]?.ToString() ?? "",
                    GSTIN = r["GSTIN"]?.ToString() ?? "",
                    LicenseNumber = r["LicenseNumber"]?.ToString() ?? "",
                    BankName = r["BankName"]?.ToString() ?? "",
                    AccountName = r["AccountName"]?.ToString() ?? "",
                    AccountNumber = r["AccountNumber"]?.ToString() ?? "",
                    IFSCCode = r["IFSCCode"]?.ToString() ?? "",
                    UpiId = r["UpiId"]?.ToString() ?? "",
                    TermsAndConditions = r["TermsAndConditions"]?.ToString() ?? "",
                    FooterMessage = r["FooterMessage"]?.ToString() ?? "",
                    UpdatedAt = string.IsNullOrWhiteSpace(r["UpdatedAt"]?.ToString()) ? DateTime.Now : DateTime.Parse(r["UpdatedAt"].ToString())
                };
            }

            // Return default
            return new CompanySettings();
        }

        public static void SaveCompanySettings(CompanySettings s)
        {
            using var conn = GetConnection();
            var chkCmd = conn.CreateCommand();
            chkCmd.CommandText = "SELECT COUNT(*) FROM CompanySettings";
            long count = (long)chkCmd.ExecuteScalar();

            var cmd = conn.CreateCommand();
            if (count == 0)
            {
                cmd.CommandText = @"INSERT INTO CompanySettings (ShopName, ShopAddress, ShopPhone, GSTIN, LicenseNumber, BankName, AccountName, AccountNumber, IFSCCode, UpiId, TermsAndConditions, FooterMessage, UpdatedAt)
                    VALUES ($sn, $sa, $sp, $gst, $lic, $bn, $an, $acc, $ifsc, $upi, $tc, $fm, $u);";
            }
            else
            {
                cmd.CommandText = @"UPDATE CompanySettings SET ShopName=$sn, ShopAddress=$sa, ShopPhone=$sp, GSTIN=$gst, LicenseNumber=$lic, BankName=$bn, AccountName=$an, AccountNumber=$acc, IFSCCode=$ifsc, UpiId=$upi, TermsAndConditions=$tc, FooterMessage=$fm, UpdatedAt=$u WHERE Id=1;";
            }

            cmd.Parameters.AddWithValue("$sn", s.ShopName ?? "");
            cmd.Parameters.AddWithValue("$sa", s.ShopAddress ?? "");
            cmd.Parameters.AddWithValue("$sp", s.ShopPhone ?? "");
            cmd.Parameters.AddWithValue("$gst", s.GSTIN ?? "");
            cmd.Parameters.AddWithValue("$lic", s.LicenseNumber ?? "");
            cmd.Parameters.AddWithValue("$bn", s.BankName ?? "");
            cmd.Parameters.AddWithValue("$an", s.AccountName ?? "");
            cmd.Parameters.AddWithValue("$acc", s.AccountNumber ?? "");
            cmd.Parameters.AddWithValue("$ifsc", s.IFSCCode ?? "");
            cmd.Parameters.AddWithValue("$upi", s.UpiId ?? "");
            cmd.Parameters.AddWithValue("$tc", s.TermsAndConditions ?? "");
            cmd.Parameters.AddWithValue("$fm", s.FooterMessage ?? "");
            cmd.Parameters.AddWithValue("$u", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        // ===================== Stock Adjustments =====================

        public static void SaveStockAdjustment(StockAdjustment adj)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Fetch current stock
                var getCmd = conn.CreateCommand();
                getCmd.CommandText = "SELECT StockQty, Name, BatchNo FROM Products WHERE Id=$pid";
                getCmd.Parameters.AddWithValue("$pid", adj.ProductId);
                using var r = getCmd.ExecuteReader();
                if (!r.Read()) throw new Exception("Product not found.");

                int currStock = Convert.ToInt32(r["StockQty"]);
                string pName = r["Name"]?.ToString();
                string bNo = r["BatchNo"]?.ToString();
                r.Close();

                adj.PreviousQty = currStock;
                adj.ProductName = pName;
                adj.BatchNumber = bNo;

                if (adj.AdjustmentType == "ADD")
                {
                    adj.NewQty = currStock + adj.DeltaQty;
                }
                else if (adj.AdjustmentType == "REDUCE")
                {
                    adj.NewQty = Math.Max(0, currStock - adj.DeltaQty);
                    adj.DeltaQty = currStock - adj.NewQty; // actual delta subtracted
                }
                else if (adj.AdjustmentType == "SET")
                {
                    adj.NewQty = Math.Max(0, adj.NewQty);
                    adj.DeltaQty = adj.NewQty - currStock;
                }

                // 2. Update stock on product
                var updCmd = conn.CreateCommand();
                updCmd.CommandText = "UPDATE Products SET StockQty = $nq WHERE Id = $pid";
                updCmd.Parameters.AddWithValue("$nq", adj.NewQty);
                updCmd.Parameters.AddWithValue("$pid", adj.ProductId);
                updCmd.ExecuteNonQuery();

                // 3. Insert audit record
                var insCmd = conn.CreateCommand();
                insCmd.CommandText = @"INSERT INTO StockAdjustments (ProductId, ProductName, BatchNumber, PreviousQty, NewQty, DeltaQty, AdjustmentType, Reason, Notes, AdjustedBy, CreatedAt)
                    VALUES ($pid, $pn, $bn, $pq, $nq, $dq, $at, $rs, $nt, $ab, $ca);";
                insCmd.Parameters.AddWithValue("$pid", adj.ProductId);
                insCmd.Parameters.AddWithValue("$pn", adj.ProductName ?? "");
                insCmd.Parameters.AddWithValue("$bn", adj.BatchNumber ?? "");
                insCmd.Parameters.AddWithValue("$pq", adj.PreviousQty);
                insCmd.Parameters.AddWithValue("$nq", adj.NewQty);
                insCmd.Parameters.AddWithValue("$dq", adj.DeltaQty);
                insCmd.Parameters.AddWithValue("$at", adj.AdjustmentType ?? "SET");
                insCmd.Parameters.AddWithValue("$rs", adj.Reason ?? "");
                insCmd.Parameters.AddWithValue("$nt", adj.Notes ?? "");
                insCmd.Parameters.AddWithValue("$ab", adj.AdjustedBy ?? "Admin");
                insCmd.Parameters.AddWithValue("$ca", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insCmd.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static (List<StockAdjustment> Items, int Total) GetStockAdjustmentsPaged(string search = null, int page = 1, int pageSize = 20)
        {
            var list = new List<StockAdjustment>();
            using var conn = GetConnection();
            var where = "1=1";
            if (!string.IsNullOrWhiteSpace(search))
                where = "(ProductName LIKE $s OR BatchNumber LIKE $s OR Reason LIKE $s)";

            var cntCmd = conn.CreateCommand();
            cntCmd.CommandText = $"SELECT COUNT(*) FROM StockAdjustments WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) cntCmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            int total = Convert.ToInt32((long)cntCmd.ExecuteScalar());

            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = $"SELECT * FROM StockAdjustments WHERE {where} ORDER BY AdjustmentId DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) dataCmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            dataCmd.Parameters.AddWithValue("$limit", pageSize);
            dataCmd.Parameters.AddWithValue("$offset", offset);
            using var r = dataCmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new StockAdjustment
                {
                    AdjustmentId = Convert.ToInt32(r["AdjustmentId"]),
                    ProductId = Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString(),
                    BatchNumber = r["BatchNumber"]?.ToString(),
                    PreviousQty = Convert.ToInt32(r["PreviousQty"]),
                    NewQty = Convert.ToInt32(r["NewQty"]),
                    DeltaQty = Convert.ToInt32(r["DeltaQty"]),
                    AdjustmentType = r["AdjustmentType"]?.ToString(),
                    Reason = r["Reason"]?.ToString(),
                    Notes = r["Notes"]?.ToString(),
                    AdjustedBy = r["AdjustedBy"]?.ToString(),
                    CreatedAt = DateTime.Parse(r["CreatedAt"].ToString())
                });
            }
            return (list, total);
        }

        // ===================== Enhanced Invoices & Bill History =====================

        public static (List<Invoice> Items, int Total, decimal TotalSubTotal, decimal TotalGst, decimal TotalGrand) GetInvoicesPaged(
            string search = null, int customerId = 0, int farmerId = 0, string paymentMethod = null, string dateRange = "All Time",
            DateTime? customStart = null, DateTime? customEnd = null, int page = 1, int pageSize = 20)
        {
            var list = new List<Invoice>();
            using var conn = GetConnection();
            var whereClauses = new List<string> { "1=1" };

            if (!string.IsNullOrWhiteSpace(search))
                whereClauses.Add("(i.InvoiceNo LIKE $s OR c.Name LIKE $s OR c.Phone LIKE $s OR i.CustomerName LIKE $s OR i.MobileNumber LIKE $s)");

            if (customerId > 0)
                whereClauses.Add("i.CustomerId = $cid");

            if (farmerId > 0)
                whereClauses.Add("i.FarmerId = $fid");

            if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "All")
                whereClauses.Add("i.PaymentMethod = $pm");

            // Date filtering (supports 5+ years)
            DateTime now = DateTime.Now;
            if (dateRange == "Today")
            {
                whereClauses.Add("date(i.InvoiceDate) = date('now','localtime')");
            }
            else if (dateRange == "This Week")
            {
                whereClauses.Add("date(i.InvoiceDate) >= date('now','localtime','-7 days')");
            }
            else if (dateRange == "This Month")
            {
                whereClauses.Add("date(i.InvoiceDate) >= date('now','localtime','start of month')");
            }
            else if (dateRange == "Last 365 Days")
            {
                whereClauses.Add("date(i.InvoiceDate) >= date('now','localtime','-1 year')");
            }
            else if (dateRange == "Last 5 Years")
            {
                whereClauses.Add("date(i.InvoiceDate) >= date('now','localtime','-5 years')");
            }
            else if (dateRange == "Custom" && customStart.HasValue && customEnd.HasValue)
            {
                whereClauses.Add("datetime(i.InvoiceDate) >= $cs AND datetime(i.InvoiceDate) <= $ce");
            }

            var where = string.Join(" AND ", whereClauses);

            // Summary Totals
            var sumCmd = conn.CreateCommand();
            sumCmd.CommandText = $@"SELECT COUNT(*), IFNULL(SUM(i.SubTotal),0), IFNULL(SUM(i.GstAmount),0), IFNULL(SUM(i.GrandTotal),0)
                FROM Invoices i LEFT JOIN Customers c ON i.CustomerId = c.Id WHERE {where}";
            if (!string.IsNullOrWhiteSpace(search)) sumCmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            if (customerId > 0) sumCmd.Parameters.AddWithValue("$cid", customerId);
            if (farmerId > 0) sumCmd.Parameters.AddWithValue("$fid", farmerId);
            if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "All") sumCmd.Parameters.AddWithValue("$pm", paymentMethod);
            if (dateRange == "Custom" && customStart.HasValue && customEnd.HasValue)
            {
                sumCmd.Parameters.AddWithValue("$cs", customStart.Value.ToString("yyyy-MM-dd 00:00:00"));
                sumCmd.Parameters.AddWithValue("$ce", customEnd.Value.ToString("yyyy-MM-dd 23:59:59"));
            }

            using var sumReader = sumCmd.ExecuteReader();
            int total = 0;
            decimal totalSub = 0m, totalGst = 0m, totalGrand = 0m;
            if (sumReader.Read())
            {
                total = Convert.ToInt32(sumReader.GetInt64(0));
                totalSub = Convert.ToDecimal(sumReader.GetDouble(1));
                totalGst = Convert.ToDecimal(sumReader.GetDouble(2));
                totalGrand = Convert.ToDecimal(sumReader.GetDouble(3));
            }
            sumReader.Close();

            // Paged Items
            var offset = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);
            var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = $@"SELECT i.*, c.Name as CustomerName, f.FarmerName as FarmerName, f.MobileNumber as FarmerMobile, f.VillageName as FarmerVillage
                FROM Invoices i
                LEFT JOIN Customers c ON i.CustomerId = c.Id
                LEFT JOIN Farmers f ON i.FarmerId = f.FarmerId
                WHERE {where}
                ORDER BY i.Id DESC LIMIT $limit OFFSET $offset";
            if (!string.IsNullOrWhiteSpace(search)) dataCmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            if (customerId > 0) dataCmd.Parameters.AddWithValue("$cid", customerId);
            if (farmerId > 0) dataCmd.Parameters.AddWithValue("$fid", farmerId);
            if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "All") dataCmd.Parameters.AddWithValue("$pm", paymentMethod);
            if (dateRange == "Custom" && customStart.HasValue && customEnd.HasValue)
            {
                dataCmd.Parameters.AddWithValue("$cs", customStart.Value.ToString("yyyy-MM-dd 00:00:00"));
                dataCmd.Parameters.AddWithValue("$ce", customEnd.Value.ToString("yyyy-MM-dd 23:59:59"));
            }
            dataCmd.Parameters.AddWithValue("$limit", pageSize);
            dataCmd.Parameters.AddWithValue("$offset", offset);

            using var r = dataCmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Invoice
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    CustomerId = r["CustomerId"] is DBNull ? 0 : Convert.ToInt32(r["CustomerId"]),
                    FarmerId = r["FarmerId"] is DBNull ? 0 : Convert.ToInt32(r["FarmerId"]),
                    FarmerName = r["FarmerName"]?.ToString() ?? (r["CustomerName"]?.ToString() ?? "Walk-in Customer"),
                    CustomerName = r["CustomerName"]?.ToString() ?? "",
                    MobileNumber = r["FarmerMobile"]?.ToString() ?? r["MobileNumber"]?.ToString(),
                    VillageName = r["FarmerVillage"]?.ToString() ?? r["VillageName"]?.ToString(),
                    InvoiceDate = DateTime.Parse(r["InvoiceDate"].ToString()),
                    SubTotal = Convert.ToDecimal(r["SubTotal"]),
                    GstAmount = Convert.ToDecimal(r["GstAmount"]),
                    GrandTotal = Convert.ToDecimal(r["GrandTotal"]),
                    PaymentMethod = r["PaymentMethod"]?.ToString() ?? "Cash"
                });
            }

            return (list, total, totalSub, totalGst, totalGrand);
        }

        public static string GenerateNextProductCode()
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT IFNULL(MAX(Id), 0) + 1 FROM Products";
            long nextId = Convert.ToInt64(cmd.ExecuteScalar());
            return $"PRD-{nextId:D4}";
        }

        public static List<PurchaseItem> GetPurchaseItemsForProductSelection()
        {
            var list = new List<PurchaseItem>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT pi.PurchaseItemId, pi.PurchaseId, pi.ProductId, pi.ProductName, pi.Company, 
                                       pi.BatchNumber, pi.ExpiryDate, pi.Quantity, pi.FreeQuantity, pi.PurchasePrice, pi.GST,
                                       IFNULL(pi.HSN,'') as HSN, IFNULL(pi.CategoryName,'') as CategoryName
                                FROM PurchaseItems pi
                                WHERE (pi.ProductId IS NULL OR pi.ProductId = 0)
                                ORDER BY pi.PurchaseItemId DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PurchaseItem
                {
                    PurchaseItemId = Convert.ToInt32(r["PurchaseItemId"]),
                    PurchaseId = r["PurchaseId"] is DBNull ? 0 : Convert.ToInt32(r["PurchaseId"]),
                    ProductId = r["ProductId"] is DBNull ? 0 : Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString() ?? "",
                    Company = r["Company"]?.ToString() ?? "",
                    BatchNumber = r["BatchNumber"]?.ToString() ?? "",
                    ExpiryDate = r["ExpiryDate"] is DBNull || string.IsNullOrWhiteSpace(r["ExpiryDate"]?.ToString()) ? (DateTime?)null : DateTime.Parse(r["ExpiryDate"].ToString()),
                    Quantity = r["Quantity"] is DBNull ? 0 : Convert.ToInt32(r["Quantity"]),
                    FreeQuantity = r["FreeQuantity"] is DBNull ? 0 : Convert.ToInt32(r["FreeQuantity"]),
                    PurchasePrice = r["PurchasePrice"] is DBNull ? 0m : Convert.ToDecimal(r["PurchasePrice"]),
                    GST = r["GST"] is DBNull ? 0m : Convert.ToDecimal(r["GST"]),
                    HSN = r["HSN"]?.ToString() ?? "",
                    CategoryName = r["CategoryName"]?.ToString() ?? ""
                });
            }
            return list;
        }

        public static void UpdatePurchaseItemProductId(int purchaseItemId, int productId)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE PurchaseItems SET ProductId = $pid WHERE PurchaseItemId = $piid";
            cmd.Parameters.AddWithValue("$pid", productId);
            cmd.Parameters.AddWithValue("$piid", purchaseItemId);
            cmd.ExecuteNonQuery();
        }

        public static Invoice GetInvoiceById(int id)
        {
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT i.* FROM Invoices i WHERE i.Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                bool HasCol(string col) { try { r.GetOrdinal(col); return true; } catch { return false; } }
                return new Invoice
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    InvoiceNo = r["InvoiceNo"]?.ToString(),
                    PaperBillNo = HasCol("PaperBillNo") ? r["PaperBillNo"]?.ToString() : "",
                    CustomerId = r["CustomerId"] is DBNull ? 0 : Convert.ToInt32(r["CustomerId"]),
                    FarmerId = HasCol("FarmerId") && !(r["FarmerId"] is DBNull) ? Convert.ToInt32(r["FarmerId"]) : 0,
                    CustomerName = HasCol("CustomerName") ? r["CustomerName"]?.ToString() ?? "Walk-in Customer" : "Walk-in Customer",
                    MobileNumber = HasCol("MobileNumber") ? r["MobileNumber"]?.ToString() ?? "" : "",
                    VillageName = HasCol("VillageName") ? r["VillageName"]?.ToString() ?? "" : "",
                    InvoiceDate = DateTime.Parse(r["InvoiceDate"].ToString()),
                    SubTotal = Convert.ToDecimal(r["SubTotal"]),
                    Discount = HasCol("Discount") && !(r["Discount"] is DBNull) ? Convert.ToDecimal(r["Discount"]) : 0,
                    TaxableAmount = HasCol("TaxableAmount") && !(r["TaxableAmount"] is DBNull) ? Convert.ToDecimal(r["TaxableAmount"]) : 0,
                    GstAmount = Convert.ToDecimal(r["GstAmount"]),
                    RoundOff = HasCol("RoundOff") && !(r["RoundOff"] is DBNull) ? Convert.ToDecimal(r["RoundOff"]) : 0,
                    GrandTotal = Convert.ToDecimal(r["GrandTotal"]),
                    PaymentMethod = r["PaymentMethod"]?.ToString() ?? "Cash",
                    PaidAmount = HasCol("PaidAmount") && !(r["PaidAmount"] is DBNull) ? Convert.ToDecimal(r["PaidAmount"]) : 0,
                    PayableAmount = HasCol("PayableAmount") && !(r["PayableAmount"] is DBNull) ? Convert.ToDecimal(r["PayableAmount"]) : 0,
                    PaymentReference = HasCol("PaymentReference") ? r["PaymentReference"]?.ToString() ?? "" : "",
                    Notes = HasCol("Notes") ? r["Notes"]?.ToString() ?? "" : "",
                    Status = HasCol("Status") ? r["Status"]?.ToString() ?? "Active" : "Active"
                };
            }
            return null;
        }

        public static List<InvoiceItem> GetInvoiceItems(int invoiceId)
        {
            var list = new List<InvoiceItem>();
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT ii.*, p.Name as ProductName FROM InvoiceItems ii
                LEFT JOIN Products p ON ii.ProductId = p.Id WHERE ii.InvoiceId = $iid";
            cmd.Parameters.AddWithValue("$iid", invoiceId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new InvoiceItem
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    InvoiceId = Convert.ToInt32(r["InvoiceId"]),
                    ProductId = Convert.ToInt32(r["ProductId"]),
                    ProductName = r["ProductName"]?.ToString(),
                    Qty = Convert.ToInt32(r["Qty"]),
                    Rate = Convert.ToDecimal(r["Rate"]),
                    GstPercent = Convert.ToDecimal(r["GstPercent"]),
                    Amount = Convert.ToDecimal(r["Amount"])
                });
            }
            return list;
        }

        /// <summary>
        /// Updates an existing invoice and adjusts stock deltas for modified items.
        /// </summary>
        /// 

        public static List<Farmer> GetFarmerNameSuggestions(string search)
        {
            var list = new List<Farmer>();
            if (string.IsNullOrWhiteSpace(search)) return list;
            using var conn = GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT FarmerId, FarmerName, MobileNumber, VillageName 
                         FROM Farmers 
                         WHERE FarmerName LIKE $s 
                         ORDER BY FarmerName LIMIT 8";
            cmd.Parameters.AddWithValue("$s", $"%{search.Trim()}%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Farmer
                {
                    FarmerId = r.GetInt32(0),
                    FarmerName = r["FarmerName"]?.ToString(),
                    MobileNumber = r["MobileNumber"]?.ToString(),
                    VillageName = r["VillageName"]?.ToString()
                });
            }
            return list;
        }
        public static void UpdateInvoice(Invoice invoice, List<InvoiceItem> newItems)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Fetch old invoice items to reverse stock
                var oldItems = new List<InvoiceItem>();
                var oldCmd = conn.CreateCommand();
                oldCmd.CommandText = "SELECT ProductId, Qty FROM InvoiceItems WHERE InvoiceId = $iid";
                oldCmd.Parameters.AddWithValue("$iid", invoice.Id);
                using (var r = oldCmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        oldItems.Add(new InvoiceItem
                        {
                            ProductId = Convert.ToInt32(r["ProductId"]),
                            Qty = Convert.ToInt32(r["Qty"])
                        });
                    }
                }

                // 2. Restore stock for old items
                foreach (var oldIt in oldItems)
                {
                    AdjustStock(oldIt.ProductId, oldIt.Qty, conn);
                }

                // 3. Delete old items
                var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM InvoiceItems WHERE InvoiceId = $iid";
                delCmd.Parameters.AddWithValue("$iid", invoice.Id);
                delCmd.ExecuteNonQuery();

                // 4. Update Header
                var updCmd = conn.CreateCommand();
                // Update key header columns. Use NULL for CustomerId when it's 0 to avoid FK violations.
                updCmd.CommandText = @"UPDATE Invoices SET CustomerId=$cid, FarmerId=$fid, SubTotal=$sub, Discount=$disc, TaxableAmount=$tax, GstAmount=$gst, PaidAmount=$paid, PayableAmount=$payable, PaymentMethod=$pm, PaymentReference=$pref, Notes=$notes, Status=$st, GrandTotal=$grand WHERE Id=$id";
                updCmd.Parameters.AddWithValue("$cid", invoice.CustomerId > 0 ? (object)invoice.CustomerId : DBNull.Value);
                updCmd.Parameters.AddWithValue("$fid", invoice.FarmerId);
                updCmd.Parameters.AddWithValue("$disc", invoice.Discount);
                updCmd.Parameters.AddWithValue("$tax", invoice.TaxableAmount);
                updCmd.Parameters.AddWithValue("$paid", invoice.PaidAmount);
                updCmd.Parameters.AddWithValue("$payable", invoice.PayableAmount);
                updCmd.Parameters.AddWithValue("$pref", invoice.PaymentReference ?? "");
                updCmd.Parameters.AddWithValue("$notes", invoice.Notes ?? "");
                updCmd.Parameters.AddWithValue("$st", string.IsNullOrWhiteSpace(invoice.Status) ? "Active" : invoice.Status);
                updCmd.Parameters.AddWithValue("$sub", invoice.SubTotal);
                updCmd.Parameters.AddWithValue("$gst", invoice.GstAmount);
                updCmd.Parameters.AddWithValue("$grand", invoice.GrandTotal);
                updCmd.Parameters.AddWithValue("$pm", invoice.PaymentMethod ?? "Cash");
                updCmd.Parameters.AddWithValue("$id", invoice.Id);
                updCmd.ExecuteNonQuery();

                // 5. Insert new items & deduct stock
                // Validate new items reference existing products and available stock before inserting
                foreach (var it in newItems)
                {
                    var chkCmd = conn.CreateCommand();
                    chkCmd.CommandText = "SELECT Name, StockQty, ExpiryDate FROM Products WHERE Id=$pid";
                    chkCmd.Parameters.AddWithValue("$pid", it.ProductId);
                    using var rr = chkCmd.ExecuteReader();
                    if (!rr.Read())
                    {
                        throw new Exception($"Product with Id {it.ProductId} ('{it.ProductName}') not found. Cannot add invoice item referencing non-existent product.");
                    }
                    int liveStock = Convert.ToInt32(rr["StockQty"]);
                    DateTime? expDate = rr["ExpiryDate"] is DBNull ? (DateTime?)null : DateTime.Parse(rr["ExpiryDate"].ToString());
                    rr.Close();

                    if (expDate.HasValue && expDate.Value.Date < DateTime.Today)
                        throw new Exception($"Product batch for '{it.ProductName}' has expired ({expDate.Value:dd MMM yyyy}) and cannot be sold.");

                    if (it.Qty > liveStock)
                        throw new Exception($"Insufficient stock for '{it.ProductName}'. Available: {liveStock}, Requested: {it.Qty}.");

                    var ic = conn.CreateCommand();
                    ic.CommandText = @"INSERT INTO InvoiceItems (InvoiceId, ProductId, Qty, Rate, GstPercent, Amount)
                        VALUES ($iid, $pid, $q, $r, $g, $a)";
                    ic.Parameters.AddWithValue("$iid", invoice.Id);
                    ic.Parameters.AddWithValue("$pid", it.ProductId);
                    ic.Parameters.AddWithValue("$q", it.Qty);
                    ic.Parameters.AddWithValue("$r", it.Rate);
                    ic.Parameters.AddWithValue("$g", it.GstPercent);
                    ic.Parameters.AddWithValue("$a", it.Amount);
                    ic.ExecuteNonQuery();

                    // Deduct stock for new qty
                    AdjustStock(it.ProductId, -it.Qty, conn);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static bool DeleteInvoice(int invoiceId)
        {
            using var conn = GetConnection();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Restore product stock for sold items
                var getItemsCmd = conn.CreateCommand();
                getItemsCmd.Transaction = tx;
                getItemsCmd.CommandText = "SELECT ProductId, Qty FROM InvoiceItems WHERE InvoiceId = $id";
                getItemsCmd.Parameters.AddWithValue("$id", invoiceId);
                using (var reader = getItemsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                        {
                            int prodId = reader.GetInt32(0);
                            double qty = Convert.ToDouble(reader.GetValue(1));
                            var updateStockCmd = conn.CreateCommand();
                            updateStockCmd.Transaction = tx;
                            updateStockCmd.CommandText = "UPDATE Products SET CurrentStock = CurrentStock + $q WHERE Id = $pid";
                            updateStockCmd.Parameters.AddWithValue("$q", qty);
                            updateStockCmd.Parameters.AddWithValue("$pid", prodId);
                            updateStockCmd.ExecuteNonQuery();
                        }
                    }
                }

                // 2. Delete invoice items
                var delItemsCmd = conn.CreateCommand();
                delItemsCmd.Transaction = tx;
                delItemsCmd.CommandText = "DELETE FROM InvoiceItems WHERE InvoiceId = $id";
                delItemsCmd.Parameters.AddWithValue("$id", invoiceId);
                delItemsCmd.ExecuteNonQuery();

                // 3. Delete invoice header
                var delInvCmd = conn.CreateCommand();
                delInvCmd.Transaction = tx;
                delInvCmd.CommandText = "DELETE FROM Invoices WHERE Id = $id";
                delInvCmd.Parameters.AddWithValue("$id", invoiceId);
                delInvCmd.ExecuteNonQuery();

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logger.Log(ex);
                return false;
            }
        }
    }
}