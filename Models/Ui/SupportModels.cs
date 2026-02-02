using DoorMonitorSystem.Models.ConfigEntity;
using System;

namespace DoorMonitorSystem.Models.Ui
{
    /// <summary>
    /// 用于批量生成向导的组项
    /// </summary>
    /// <summary>
    /// 批量生成向导中的“组”选择项模型。
    /// 用于在 UI 上展示可供选择的 DoorGroup 或 PanelGroup，供用户勾选以批量生成点位。
    /// </summary>
    public class BatchGroupItem
    {
        /// <summary>
        /// 组显示名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 组的业务 ID (KeyId)。
        /// 对应 DoorGroup.KeyId 或 PanelGroup.KeyId。
        /// </summary>
        public string KeyId { get; set; } 

        /// <summary>
        /// 目标类型 (Door 或 Panel)。
        /// </summary>
        public TargetType TargetType { get; set; }

        /// <summary>
        /// 是否被用户选中。
        /// </summary>
        public bool IsSelected { get; set; }
    }
}
