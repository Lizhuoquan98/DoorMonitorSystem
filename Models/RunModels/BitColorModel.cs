using System.Windows.Media;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 颜色运行时模型
    /// 从数据库加载后，将颜色值转换为Brush对象
    /// </summary>
    public class BitColorModel : NotifyPropertyChanged
    {
        /// <summary>颜色ID</summary>
        public int ColorId { get; set; }

        /// <summary>颜色名称</summary>
        private string _colorName = "";
        public string ColorName
        {
            get => _colorName;
            set
            {
                _colorName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>颜色值（十六进制字符串）</summary>
        private string _colorValue = "";
        public string ColorValue
        {
            get => _colorValue;
            set
            {
                _colorValue = value;
                OnPropertyChanged();
                UpdateBrush();
            }
        }

        /// <summary>颜色Brush对象（用于UI绑定）</summary>
        private Brush _colorBrush = Brushes.Gray;
        public Brush ColorBrush
        {
            get => _colorBrush;
            private set
            {
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>备注</summary>
        public string Remark { get; set; } = "";

        /// <summary>
        /// 更新Brush对象
        /// 将颜色值字符串（#RRGGBB）转换为Brush
        /// </summary>
        private void UpdateBrush()
        {
            try
            {
                if (!string.IsNullOrEmpty(ColorValue))
                {
                    // 将 #RRGGBB 格式转换为 Brush
                    var color = (Color)ColorConverter.ConvertFromString(ColorValue);
                    ColorBrush = new SolidColorBrush(color);
                }
                else
                {
                    ColorBrush = Brushes.Gray;
                }
            }
            catch
            {
                ColorBrush = Brushes.Gray;
            }
        }

        /// <summary>
        /// 从颜色值创建Brush（静态辅助方法）
        /// </summary>
        public static Brush CreateBrushFromColorValue(string colorValue)
        {
            try
            {
                if (!string.IsNullOrEmpty(colorValue))
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorValue);
                    return new SolidColorBrush(color);
                }
            }
            catch
            {
                // 转换失败，返回默认颜色
            }
            return Brushes.Gray;
        }
    }
}
