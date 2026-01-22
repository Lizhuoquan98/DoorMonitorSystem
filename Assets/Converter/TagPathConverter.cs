using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DoorMonitorSystem.Assets.Converter
{
    public class TagPathConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)  
        {
            // 过滤掉 null 或空字符串，只保留有效值
            var pathParts = values
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                .Select(v => v.ToString());

            // 使用 '/' 拼接路径
            return string.Join(".", pathParts);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
