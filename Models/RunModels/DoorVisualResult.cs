using ControlLibrary.Models;
using DoorMonitorSystem.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private List<IconItem> _icons = new();
        public List<IconItem> Icons
        {
            get => _icons;
            set { _icons = value; OnPropertyChanged(); }
        }

        private Brush _header = Brushes.LightGray;
        public Brush HeaderBackground
        {
            get => _header;
            set { _header = value; OnPropertyChanged(); }
        }

        private string _headerText = "";
        public string HeaderText
        {
            get => _headerText;
            set { _headerText = value; OnPropertyChanged(); }
        }

        private Brush _bottom = Brushes.LightGray;
        public Brush BottomBackground
        {
            get => _bottom;
            set { _bottom = value; OnPropertyChanged(); }
        }
    }

}
