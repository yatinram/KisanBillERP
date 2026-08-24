# Run from repository root (PowerShell)
# This will add Selenium packages to the KrushiBillERP project
dotnet add "KrushiBillERP.csproj" package Selenium.WebDriver
dotnet add "KrushiBillERP.csproj" package Selenium.Support
dotnet add "KrushiBillERP.csproj" package Selenium.WebDriver.ChromeDriver

Write-Host "Packages added. Run 'dotnet restore' or rebuild the solution in Visual Studio."