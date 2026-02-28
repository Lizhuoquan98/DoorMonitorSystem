using ControlLibrary.Models;
using DoorMonitorSystem.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 单门最终显示裁决结果（一次循环只算一次）
    /// </summary>
    public class DoorVisualResult : NotifyPropertyChanged
    {
        // 顶部小图标槽位 (用于显示辅助状态)
        private IconItem _iconTop;
        public IconItem IconTop
        {
            get => _iconTop;
            set { if (_iconTop == value) return; _iconTop = value; OnPropertyChanged(); }
        }

        // 中间主图标槽位 (显示门体开关状态的核心矢量图)
        private IconItem _iconMid;
        public IconItem IconMid
        {
            get => _iconMid;
            set { if (_iconMid == value) return; _iconMid = value; OnPropertyChanged(); }
        }

        // 底部小图标槽位 (用于显示安全回路等状态)
        private IconItem _iconBot;
        public IconItem IconBot
        {
            get => _iconBot;
            set { if (_iconBot == value) return; _iconBot = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 视觉图标集合（用于绑定到 DoorControl）
        /// </summary>
        private IEnumerable<IconItem> _iconItems = Array.Empty<IconItem>();
        public IEnumerable<IconItem> IconItems 
        { 
            get => _iconItems; 
            set { if (_iconItems == value) return; _iconItems = value; OnPropertyChanged(); } 
        }


        private Brush _header = Brushes.LightGray;
        public Brush HeaderBackground
        {
            get => _header;
            set 
            { 
                if (_header == value) return;
                _header = value; 
                OnPropertyChanged(); 
            }
        }

        private string _headerText = "";
        public string HeaderText
        {
            get => _headerText;
            set 
            { 
                if (_headerText == value) return;
                _headerText = value; 
                OnPropertyChanged(); 
            }
        }

        private Brush _bottom = Brushes.LightGray;
        public Brush BottomBackground
        {
            get => _bottom;
            set 
            { 
                if (_bottom == value) return;
                _bottom = value; 
                OnPropertyChanged(); 
            }
        }
    }

}
