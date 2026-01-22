using System.Collections.ObjectModel;
using System.Linq;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.RunModels;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 站台视图模型（包装 StationMainGroup，添加UI辅助属性）
    /// </summary>
    public class StationViewModel : NotifyPropertyChanged
    {
        private readonly StationMainGroup _station;

        public StationViewModel(StationMainGroup station)
        {
            _station = station;
            RefreshDisplayItems();
        }

        /// <summary>原始站台数据</summary>
        public StationMainGroup Station => _station;

        /// <summary>
        /// 统一的显示项集合（门组和面板组混合，按各自的 SortOrder 排序）
        /// UI绑定这个集合，自动按顺序显示
        /// </summary>
        public ObservableCollection<object> DisplayItems { get; set; } = new();

        /// <summary>
        /// 刷新显示项集合
        /// 将门组和面板组合并，按 SortOrder 排序
        /// </summary>
        public void RefreshDisplayItems()
        {
            DisplayItems.Clear();

            var allItems = new System.Collections.Generic.List<(int SortOrder, object Item)>();

            // 添加所有门组
            foreach (var doorGroup in _station.DoorGroups)
            {
                allItems.Add((doorGroup.SortOrder, doorGroup));
            }

            // 添加所有面板组
            foreach (var panelGroup in _station.PanelGroups)
            {
                allItems.Add((panelGroup.SortOrder, panelGroup));
            }

            // 按 SortOrder 排序后添加到显示集合
            foreach (var item in allItems.OrderBy(x => x.SortOrder))
            {
                DisplayItems.Add(item.Item);
            }
        }
    }
}
