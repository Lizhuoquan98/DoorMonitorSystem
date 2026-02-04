using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using DoorMonitorSystem.Base;
using ControlLibrary.Models;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 门点位运行时模型（通用点位模板）
    /// 不属于具体某个门，而是按门类型分类的通用配置
    /// 滑动门、应急门、端门各有一套独立的点位模板
    /// </summary>
    public partial class DoorBitConfig : NotifyPropertyChanged
    {
        #region 基础属性

        /// <summary>点位ID（主键，点位模板的唯一标识）</summary>
        public int BitId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属门的 KeyId (运行时性能优化用)</summary>
        public string ParentDoorKeyId { get; set; } = "";

        /// <summary>点位描述，如：开门、关门、故障、锁闭等</summary>
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

        /// <summary>点位实时值（true=激活，false=未激活）</summary>
        private bool _bitValue;
        public bool BitValue
        {
            get => _bitValue;
            set
            {
                if (_bitValue == value) return;
                _bitValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IndicatorBrush));
            }
        }

        /// <summary>点位绑定的门类型（滑动门/应急门/端门）</summary>
        public DoorType BindingDoorType { get; set; }

        /// <summary>点位分类ID（关联 BitCategory 表）</summary>
        public int? CategoryId { get; set; }

        /// <summary>点位分类对象（用于弹窗分栏显示）</summary>
        private BitCategoryModel? _category;
        public BitCategoryModel? Category
        {
            get => _category;
            set
            {
                _category = value;
                OnPropertyChanged();
            }
        }

        /// <summary>排序序号（用于弹窗中点位的显示顺序）</summary>
        public int SortOrder { get; set; }

        #endregion

        #region 优先级配置

        /// <summary>头部显示优先级（值越大优先级越高，0表示不在头部显示）</summary>
        public int HeaderPriority { get; set; }

        /// <summary>中间图形显示优先级（值越大优先级越高，0表示不在中间显示）</summary>
        public int ImagePriority { get; set; }

        /// <summary>底部显示优先级（值越大优先级越高，0表示不在底部显示）</summary>
        public int BottomPriority { get; set; }

        #endregion

        #region 弹窗详情颜色（指示灯用）

        /// <summary>高电平颜色（弹窗指示灯）</summary>
        public Brush HighBrush { get; set; } = Brushes.LimeGreen;

        /// <summary>低电平颜色（弹窗指示灯）</summary>
        public Brush LowBrush { get; set; } = Brushes.DarkGray;

        /// <summary>弹窗指示灯UI绑定颜色</summary>
        public Brush IndicatorBrush => BitValue ? HighBrush : LowBrush;

        /// <summary>BitControl 专用的颜色配置对象</summary>
        public ControlLibrary.Models.BitColor ConfigColor => new ControlLibrary.Models.BitColor
        {
            High = HighBrush,
            Low = LowBrush
        };

        #endregion

        #region 门三部分显示配置

        /// <summary>头部颜色条颜色</summary>
        public Brush HeaderColor { get; set; } = Brushes.Gray;

        /// <summary>中间图形名称（从全局图形字典获取矢量图）</summary>
        public string GraphicName { get; set; } = "";

        /// <summary>中间图形颜色（矢量图着色用）</summary>
        public Brush GraphicColor { get; set; } = Brushes.Black;

        /// <summary>底部颜色条颜色</summary>
        public Brush BottomColor { get; set; } = Brushes.Green;

        #endregion
    }
}
