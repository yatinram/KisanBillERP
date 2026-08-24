using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace KrushiBillERP.Data
{
    public static class CsvExportHelper
    {
        public static void ExportToCsv<T>(IEnumerable<T> data, string defaultFileName, Func<T, string> formatLine, string headerLine)
        {
            var dlg = new SaveFileDialog
            {
                FileName = defaultFileName,
                DefaultExt = ".csv",
                Filter = "CSV Documents (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(headerLine);
                    foreach (var item in data)
                    {
                        sb.AppendLine(formatLine(item));
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Report exported successfully to:\n{dlg.FileName}", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
