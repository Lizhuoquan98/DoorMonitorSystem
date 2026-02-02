using System;
using System.Collections.ObjectModel;

namespace DoorMonitorSystem.Models.Ui
{
    /// <summary>
    /// 业务对象树节点模型。
    /// 用于在点位配置界面的“对象树”弹窗中展示层级化的业务结构（站台 -> 分组 -> 门/面板）。
    /// </summary>
    public class UiSelectorNode : DoorMonitorSystem.Base.NotifyPropertyChanged
    {
        private string _name;
        /// <summary>
        /// 节点显示名称（例如：1号站台、屏蔽门-01）。
        /// </summary>
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        /// <summary>
        /// 父节点名称（辅助信息）。
        /// </summary>
        public string ParentName { get; set; }

        /// <summary>
        /// 节点完整描述。
        /// </summary>
        public string FullDescription { get; set; }

        /// <summary>
        /// 节点类型标识。
        /// 取值范围：Station (站台), Group (分组), Door (门), Panel (面板), Config (配置项), Param (参数)等。
        /// UI 根据此值切换显示图标。
        /// </summary>
        public string NodeType { get; set; } 

        /// <summary>
        /// 关联的业务对象实例 ID (数据库 KeyId)。
        /// </summary>
        public string KeyId { get; set; } 

        /// <summary>
        /// 关联的位配置/定义 ID。
        /// 当节点为具体的功能点位（如“开关门状态”）时，指向该功能的配置模板。
        /// </summary>
        public string BitConfigKeyId { get; set; } 

        /// <summary>
        /// 绑定键名（如 UI 需要特定的 Mapping Key）。
        /// </summary>
        public string BindingKey { get; set; }

        /// <summary>
        /// 绑定角色（Read/Write）。
        /// </summary>
        public string BindingRole { get; set; }

        /// <summary>
        /// 所属分类名称。
        /// </summary>
        public string Category { get; set; }

        private bool _isExpanded;
        /// <summary>
        /// 节点在 TreeView 中是否展开。
        /// </summary>
        public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(); } }

        /// <summary>
        /// 子节点集合，实现递归层级结构。
        /// </summary>
        public ObservableCollection<UiSelectorNode> Children { get; set; } = new ObservableCollection<UiSelectorNode>();
    }
}
