using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DoorMonitorSystem.Assets.Converters
{
    public class DataTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dataType && parameter is string targetMode)
            {
                bool isBool = dataType.Equals("Bool", StringComparison.OrdinalIgnoreCase) || 
                              dataType.Equals("Bit", StringComparison.OrdinalIgnoreCase);

                if (targetMode == "Bool")
                {
                    // If target mode is Bool, show only if IsBool is true
                    return isBool ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (targetMode == "Analog")
                {
                    // If target mode is Analog, show only if IsBool is false
                    return !isBool ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
