using System;
using System.Globalization;
using System.Windows.Data;
using Sharp7;  // 你的枚举命名空间


namespace DoorMonitorSystem.Assets.Converter
{
    public class S7WordLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intVal)
            {
               intVal += 1;
                if (Enum.IsDefined(typeof(S7WordLength), intVal))
                {
                    var enumVal = (S7WordLength)intVal;
                    return enumVal.ToString();
                }
                else
                {
                    return "未知";
                }
            }
            return "无效";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 一般 DataGrid 不需要双向转换
            if (value is string strVal)
            {
                if (Enum.TryParse<S7WordLength>(strVal, out var enumVal))
                    return (int)enumVal;
            }
            return 0;
        }
    }
}
