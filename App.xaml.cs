using System.Windows;
using KrushiBillERP.Data;

namespace KrushiBillERP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ensure the SQLite database and tables exist before any window opens.
            DatabaseHelper.Initialize();
            // Global exception handlers: log to file and show a message box.
            this.DispatcherUnhandledException += (s, ex) =>
            {
                try
                {
                    KrushiBillERP.Data.Logger.Log(ex.Exception);
                    MessageBox.Show($"Unhandled UI exception:\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
                ex.Handled = true;
            };

            System.AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                try
                {
                    var err = ex.ExceptionObject as System.Exception;
                    KrushiBillERP.Data.Logger.Log(err);
                    MessageBox.Show($"Unhandled exception:\n{err?.Message}\n\n{err?.StackTrace}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                try
                {
                    KrushiBillERP.Data.Logger.Log(ex.Exception);
                    MessageBox.Show($"Unobserved task exception:\n{ex.Exception.Message}\n\n{ex.Exception.StackTrace}", "Task Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };
        }
    }
}
