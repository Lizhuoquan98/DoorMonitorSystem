using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DoorMonitorSystem.Assets.Converter
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Collections.Generic;

    public class DictIdToNameConverter : IValueConverter
    {
        // 这里可以注入你的字典集合，或者通过静态属性访问
        public static Dictionary<int, string>? AlarmModeDict { get; set; }
        public static Dictionary<int, string>? RecordLevelDict { get; set; }
        public static Dictionary<int, string>? BitDescriptionDict { get; set; }

        // 在 XAML 里用 ConverterParameter 指定字典名，比如 "AlarmMode" / "RecordLevel"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return "";

            if (!int.TryParse(value.ToString(), out int id))
                return "";

            string dictName = parameter.ToString() ?? "";

            Dictionary<int, string>? dict = dictName switch
            {
                "AlarmMode" => AlarmModeDict,
                "RecordLevel" => RecordLevelDict,
                "BitDescription" => BitDescriptionDict,
                _ => null,
            };

            if (dict != null && dict.TryGetValue(id+1, out string name))
                return name;

            return $"未知({id})";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 一般 DataGrid 只读显示可不实现
            return Binding.DoNothing;
        }
    }

}
