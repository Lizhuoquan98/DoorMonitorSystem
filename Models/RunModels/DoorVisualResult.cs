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
        private ObservableCollection<IconItem> _icons = new();
        /// <summary>
        /// 中间图形集合。
        /// 注意：UI 绑定此集合，应尽量避免直接重新赋值，而是使用 UpdateIcons 进行增量更新。
        /// </summary>
        public ObservableCollection<IconItem> Icons
        {
            get => _icons;
            set { _icons = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 增量更新图标集合，避免 UI 全量重建
        /// </summary>
        public void UpdateIcons(List<IconItem> newItems)
        {
            if (newItems == null)
            {
                if (Icons.Count > 0) Icons.Clear();
                return;
            }

            // 如果数量或内容不一致，清空并重新填充
            // (也可以做更复杂的 Diff 算法，但 Clear+Add 对 1-3 个图标的小集合已经足够快且稳定)
            Icons.Clear();
            foreach (var item in newItems)
            {
                Icons.Add(item);
            }
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
