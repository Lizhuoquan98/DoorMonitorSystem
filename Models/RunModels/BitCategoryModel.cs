using System.Windows.Media;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 点位分类运行时模型
    /// 用于弹窗分栏显示，将点位按类别分组（故障/报警/状态等）
    /// </summary>
    public class BitCategoryModel : NotifyPropertyChanged
    {
        /// <summary>分类ID</summary>
        public int CategoryId { get; set; }

        /// <summary>分类代码（用于程序识别）</summary>
        private string _code = "";
        public string Code
        {
            get => _code;
            set
            {
                _code = value;
                OnPropertyChanged();
            }
        }

        /// <summary>分类名称（显示在弹窗栏标题）</summary>
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>图标字符（如 ⚠/🔔/ℹ 等）</summary>
        private string? _icon;
        public string? Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        /// <summary>背景颜色值（十六进制字符串）</summary>
        private string? _backgroundColor;
        public string? BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
                UpdateBackgroundBrush();
            }
        }

        /// <summary>前景颜色值（十六进制字符串）</summary>
        private string? _foregroundColor;
        public string? ForegroundColor
        {
            get => _foregroundColor;
            set
            {
                _foregroundColor = value;
                OnPropertyChanged();
                UpdateForegroundBrush();
            }
        }

        /// <summary>背景颜色Brush对象（用于UI绑定）</summary>
        private Brush _backgroundBrush = Brushes.Gray;
        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            private set
            {
                _backgroundBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>前景颜色Brush对象（用于UI绑定）</summary>
        private Brush _foregroundBrush = Brushes.White;
        public Brush ForegroundBrush
        {
            get => _foregroundBrush;
            private set
            {
                _foregroundBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>排序序号（决定弹窗中分栏的显示顺序）</summary>
        public int SortOrder { get; set; }

        /// <summary>点位布局行数（0 表示自动）</summary>
        public int LayoutRows { get; set; } = 0;

        /// <summary>点位布局列数（如 2 表示 2 列显示）</summary>
        public int LayoutColumns { get; set; } = 2;

        /// <summary>
        /// 更新背景Brush对象
        /// 将颜色值字符串（#RRGGBB）转换为Brush
        /// </summary>
        private void UpdateBackgroundBrush()
        {
            BackgroundBrush = BitColorModel.CreateBrushFromColorValue(BackgroundColor ?? "#808080");
        }

        /// <summary>
        /// 更新前景Brush对象
        /// 将颜色值字符串（#RRGGBB）转换为Brush
        /// </summary>
        private void UpdateForegroundBrush()
        {
            ForegroundBrush = BitColorModel.CreateBrushFromColorValue(ForegroundColor ?? "#FFFFFF");
        }
    }
}
