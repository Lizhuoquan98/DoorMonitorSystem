using System.Collections.ObjectModel;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 站台主组运行时模型（纯数据容器）
    /// 只负责存储门组和面板组，通过ID关联
    /// </summary>
    public class StationMainGroup
    {
        /// <summary>站台ID（主键）</summary>
        public int StationId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>站台名称</summary>
        public string StationName { get; set; } = "";

        /// <summary>站台编码</summary>
        public string StationCode { get; set; } = "";

        /// <summary>
        /// 站台类型（对应 StationType 枚举）
        /// 决定UI布局方式：
        /// - Island(1): 岛式站台，上下镜像布局
        /// - Side(2): 侧式站台，单侧垂直布局
        /// - ThreeTrack(3): 三线站台，垂直排列一模一样的UI
        /// </summary>
        public StationType StationType { get; set; } = StationType.Island;

        /// <summary>排序序号（用于多站台显示顺序）</summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// 门组列表（通过 DoorGroup.SortOrder 控制显示顺序）
        /// </summary>
        public System.Collections.Generic.List<DoorGroup> DoorGroups { get; set; } = new();

        /// <summary>
        /// 面板组列表（通过 PanelGroup.SortOrder 控制显示顺序）
        /// </summary>
        public System.Collections.Generic.List<PanelGroup> PanelGroups { get; set; } = new();
    }
}
