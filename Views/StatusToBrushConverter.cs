using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KrushiBillERP.Views
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return Brushes.Gray;
                var v = System.Convert.ToInt32(value);
                return v == 1 ? Brushes.Green : Brushes.Gray;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PaymentStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            return status switch
            {
                "Paid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")),
                "Partial" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
                "Unpaid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")),
                _ => Brushes.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PaymentStatusToBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value?.ToString();
            return status switch
            {
                "Paid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")),
                "Partial" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0")),
                "Unpaid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
