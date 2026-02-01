using System;
using System.Globalization;
using System.Windows.Data;

namespace DoorMonitorSystem.Assets.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && int.TryParse(value.ToString(), out int v) && int.TryParse(parameter?.ToString(), out int p))
            {
                return v == p;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && int.TryParse(parameter?.ToString(), out int p))
            {
                return p;
            }
            return Binding.DoNothing;
        }
    }
}
