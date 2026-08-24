Selenium WhatsApp auto-attach setup

1) Install packages (PowerShell):
   .\scripts\install-selenium.ps1

2) Build the project in Visual Studio or run `dotnet restore`.

3) ChromeDriver version vs Chrome browser:
   - The Selenium.WebDriver.ChromeDriver NuGet package usually installs a matching driver.
   - If driver version mismatches your installed Chrome, either update Chrome or install a ChromeDriver version that matches.

4) First run:
   - The code will create a `selenium_profile` folder in the app output directory to persist login.
   - On first run, a Chrome window will open and you must scan the WhatsApp QR code.
   - After that, subsequent runs will reuse the profile so you won't need to scan again.

5) Usage:
   - Call Views.InvoicePdfHelper.GeneratePdfAndAttachWhatsappAuto(invoiceId, phoneNumber).
   - The browser will open, attach the generated PDF to the chat. You must press Send.

6) Notes & troubleshooting:
   - Ensure Chrome is installed and accessible.
   - If ChromeDriver can't be found, ensure the ChromeDriver binary is present in the output. The NuGet package normally copies it.
   - If you want the driver bundled somewhere else, update PATH or copy the chromedriver.exe next to the app executable.

7) Security:
   - The selenium_profile directory stores Chrome session data. Keep it secure.
