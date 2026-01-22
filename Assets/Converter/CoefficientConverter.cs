using DoorMonitorSystem.UControl;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DoorMonitorSystem.Assets.Converter
{
    
    public class CoefficientConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is double width && parameter is string paramStr && double.TryParse(paramStr, out double coefficient))
            {
                return  width   * coefficient; // 例如：父宽度 × 0.5
            }
            return value;
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
