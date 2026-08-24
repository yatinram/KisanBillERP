# KrushiBill ERP

Seeds & Pesticides shop mate banaveli **dynamic billing/ERP software**, WPF (C#) + SQLite ma.
"Dynamic" etle - je koi pn shop aa software install kare, temna login credentials thi login thata
j temnu potanu Shop Name, Address, Products, Customers ane Bills dekhay. Kai data hardcoded nathi.

## Default Login (pehli var)
```
Username: admin
Password: admin123
```
⚠️ Pehla login pachi Users table ma jaine password/shop details badlo (future ma "Settings"
screen umeri shakay jya thi UI thi j badli shakay).

## Features (aa version ma)
- Login screen — khota credentials par error batave che
- Dashboard — Total Products, Total Customers, Aajni Sales, Low Stock Alerts
- Products module — Seeds/Pesticides/Fertilizers/Tools, Batch No, Expiry Date, GST%, Stock
- Customers module — Add/Edit/Delete
- Billing (New Bill) — product select kari cart banavo, GST auto-calculate, invoice save,
  stock auto-reduce
- Stock view — current inventory + reorder alerts
- Sales History — badha invoices ni list

## Technology
- **C# WPF (.NET 8)** — Windows desktop UI
- **SQLite** (`Microsoft.Data.Sqlite`) — ek j file (`krushibill.db`), install ni jaroor nathi
- Database file EXE ni bajuma j auto-create thay che first run par

## Kai rite chalavu (Visual Studio)
1. **.NET 8 SDK** install karo (jo nathi): https://dotnet.microsoft.com/download
2. Visual Studio 2022 (Community free che) ma `KrushiBillERP.csproj` open karo
3. Pehli var build karo — NuGet automatic `Microsoft.Data.Sqlite` package download kari lese
   (internet joiye)
4. `F5` dabao ya "Start" — software chalu thai jashe, LoginWindow khulshe

## Kai rite chalavu (Command line)
```
cd KrushiBillERP
dotnet restore
dotnet run
```

## EXE banavva mate (deployment - shop na computer par mokalva)
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Aa command thi ek j `KrushiBillERP.exe` file banse (`bin\Release\net8.0-windows\win-x64\publish\`
folder ma), jene koi pn Windows computer par direct chalavi shakay — .NET install karvani
jaroor nathi.

## Naye Shop mate setup
Naya shop ne aapva mate:
1. `krushibill.db` file delete kari do (jo hoy to) — fresh database auto-create thashe
2. Software pehli var chalavo — default admin/admin123 thi login karo
3. Products, Categories, Customers potana shop mujab umero

## Project Structure
```
KrushiBillERP/
 ├─ App.xaml / App.xaml.cs          -> startup, DB initialize
 ├─ Data/DatabaseHelper.cs          -> tamam SQLite CRUD operations
 ├─ Models/Models.cs                -> User, Product, Customer, Invoice, InvoiceItem
 ├─ Views/
 │   ├─ LoginWindow                 -> login screen
 │   ├─ DashboardWindow             -> sidebar + header (dynamic user/shop info)
 │   ├─ DashboardHomeView           -> summary cards               [BUILT]
 │   ├─ ProductsView                -> product CRUD                [BUILT]
 │   ├─ CustomersView               -> customer CRUD                [BUILT]
 │   ├─ BillingView                 -> new bill / invoice creation  [BUILT]
 │   ├─ StockView                   -> inventory list                [BUILT]
 │   ├─ InvoicesView                -> sales history                 [BUILT]
 │   ├─ SuppliersView               -> vendor CRUD                   [STUB - tame banavo]
 │   ├─ PurchaseView                -> purchase entry (stock-in)     [STUB - tame banavo]
 │   ├─ ExpensesView                -> daily expenses                [STUB - tame banavo]
 │   ├─ ReportsView                 -> sales/GST/stock reports       [STUB - tame banavo]
 │   ├─ UsersView                   -> user/role management          [STUB - tame banavo]
 │   └─ SettingsView                -> shop details/password change  [STUB - tame banavo]
 └─ Assets/logo.png, logo.ico       -> app branding
```

## [STUB] pages kai rite complete karva
Dareek stub page (`Views/*.xaml` + `.xaml.cs`) ma abhi fakt title + icon che, sidebar ma
navigation already wire thai gayu che. Aema logic bharva mate:
1. `Data/DatabaseHelper.cs` ma e module mate table (jo navi joiye to) `Initialize()` ma
   `CREATE TABLE IF NOT EXISTS` thi umero, ane CRUD methods (Get/Save/Delete) umero
   — `CustomersView`/`SuppliersView` na CRUD pattern jevu j
2. `Models/Models.cs` ma jarur hoy to navo model class umero
3. Page na `.xaml` ma DataGrid/Form banavo (`CustomersView.xaml` ne template tarike use karo)
4. Page na `.xaml.cs` ma button click handlers thi `DatabaseHelper` na methods call karo

## Aagad su umeri shakay (roadmap ideas)
- Invoice print / PDF export
- Role-based access control (Admin vs Cashier menu hide/show)
- Backup/Restore database button
- GST reports (monthly sales, HSN summary)
