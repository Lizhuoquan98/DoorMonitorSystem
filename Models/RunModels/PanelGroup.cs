using System.Collections.ObjectModel;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 面板组（面板的容器，用于分组管理）
    /// </summary>
    public class PanelGroup : NotifyPropertyChanged
    {
        /// <summary>面板组ID（主键）</summary>
        public int PanelGroupId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属站台ID（外键）</summary>
        public int StationId { get; set; }

        /// <summary>面板组排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>面板列表</summary>
        public ObservableCollection<PanelModel> Panels { get; set; } = new();
    }
}
