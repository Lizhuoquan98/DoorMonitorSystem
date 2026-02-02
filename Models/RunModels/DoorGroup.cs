using System.Collections.ObjectModel;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 门组（一组门的集合）
    /// </summary>
    public class DoorGroup : NotifyPropertyChanged
    {
        /// <summary>门组ID（主键）</summary>
        public int DoorGroupId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属站台ID（外键）</summary>
        public int StationId { get; set; }

        /// <summary>门列表</summary>
        public ObservableCollection<DoorModel> Doors { get; set; } = new();

        /// <summary>门组排序序号</summary>
        public int SortOrder { get; set; }
    }
}
