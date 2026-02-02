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

            // 根据站台类型（StationType）决定组件排列顺序
            // 岛式 (Island, 1): 门模组在上，面板模组在下
            // 侧式 (Side, 2): 面板模组在上，门模组在下
            // 三线站台 (ThreeTrack, 3): 同岛式，默认门在上

            if (_station.StationType == StationType.Side)
            {
                // 侧式站台：优先显示面板组，再显示门组
                foreach (var pg in _station.PanelGroups.OrderBy(x => x.SortOrder))
                {
                    DisplayItems.Add(pg);
                }
                foreach (var dg in _station.DoorGroups.OrderBy(x => x.SortOrder))
                {
                    DisplayItems.Add(dg);
                }
            }
            else
            {
                // 岛式或三线站台：优先显示门组，再显示面板组
                foreach (var dg in _station.DoorGroups.OrderBy(x => x.SortOrder))
                {
                    DisplayItems.Add(dg);
                }
                foreach (var pg in _station.PanelGroups.OrderBy(x => x.SortOrder))
                {
                    DisplayItems.Add(pg);
                }
            }
        }
    }
}
