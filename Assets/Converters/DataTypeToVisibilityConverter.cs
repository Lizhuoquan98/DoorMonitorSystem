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
            string targetMode = parameter as string;
            
            // 1. 特殊逻辑处理
            if (targetMode == "HasValue")
            {
                bool hasValue = value != null && !string.IsNullOrEmpty(value.ToString());
                return hasValue ? Visibility.Visible : Visibility.Collapsed;
            }
            if (targetMode == "InverseBool" && value is bool b)
            {
                return !b ? Visibility.Visible : Visibility.Collapsed;
            }

            // 2. 原有的数据类型判断逻辑
            if (value is string dataType && !string.IsNullOrEmpty(targetMode))
            {
                bool isBool = dataType.Equals("Bool", StringComparison.OrdinalIgnoreCase) || 
                              dataType.Equals("Bit", StringComparison.OrdinalIgnoreCase);

                if (targetMode == "Bool")
                {
                    return isBool ? Visibility.Visible : Visibility.Collapsed;
                }
                else if (targetMode == "Analog")
                {
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
