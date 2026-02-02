using DoorMonitorSystem.Base;
using System.Windows.Media;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 面板点位运行时模型
    /// </summary>
    public partial class PanelBitConfig : NotifyPropertyChanged
    {
        /// <summary>点位ID（主键）</summary>
        public int BitId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属面板ID（外键）</summary>
        public int PanelId { get; set; }

        /// <summary>点位描述，如：供能、启用、关门等</summary>
        private string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        /// <summary>点位实时值</summary>
        private bool _bitValue;
        public bool BitValue
        {
            get => _bitValue;
            set
            {
                if (_bitValue != value)
                {
                    _bitValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayBrush));
                }
            }
        }

        /// <summary>高电平颜色</summary>
        public Brush HighBrush { get; set; } = Brushes.LimeGreen;

        /// <summary>低电平颜色</summary>
        public Brush LowBrush { get; set; } = Brushes.DarkGray;

        /// <summary>UI直接绑定的显示颜色</summary>
        public Brush DisplayBrush => BitValue ? HighBrush : LowBrush;

        /// <summary>点位排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>BitControl 专用的颜色配置对象</summary>
        public ControlLibrary.Models.BitColor ConfigColor => new ControlLibrary.Models.BitColor 
        { 
            High = HighBrush, 
            Low = LowBrush 
        };
    }
}
