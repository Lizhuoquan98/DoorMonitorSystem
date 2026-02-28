using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.ConfigEntity;
using System.ComponentModel.DataAnnotations.Schema;
using DoorMonitorSystem.Models;

namespace DoorMonitorSystem.Models.Ui
{
    /// <summary>
    /// 点位配置展示模型 (UI Wrapper)
    /// 封装底层的 Entity，并扩展适用于 DataGrid 看板显示的动态计算属性。
    /// 遵循“关注点分离”原则，底层数据库实体仅存储原始数据，展示逻辑由此层负责。
    /// </summary>
    public class DevicePointRow : NotifyPropertyChanged
    {
        /// <summary>
        /// 底层数据库实体对象，包含所有持久化到数据库的字段。
        /// </summary>
        public DevicePointConfigEntity Entity { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="entity">关联的底层点位配置实体</param>
        public DevicePointRow(DevicePointConfigEntity entity)
        {
            Entity = entity;
            // 订阅底层实体的更改，以便同步更新 UI 计算属性（如地址格式）
            Entity.PropertyChanged += (s, e) => {
                OnPropertyChanged(nameof(FormattedAddress));
                OnPropertyChanged(nameof(FormattedSyncTarget));
            };
        }

        private int _rowIndex;
        /// <summary>
        /// 交互界面序号。
        /// 动态生成，不存入数据库，仅用于 UI 列表（DataGrid）显示。
        /// </summary>
        public int RowIndex { get => _rowIndex; set { _rowIndex = value; OnPropertyChanged(); } }

        /// <summary>
        /// 格式化后的物理地址。
        /// 针对 Bool/Bit 类型，会自动合并显示为 "地址.位索引"（例如：10.0）。
        /// 非位类型则直接显示地址。
        /// </summary>
        public string FormattedAddress
        {
            get
            {
                if (Entity.DataType != null && (Entity.DataType.Equals("Bool", System.StringComparison.OrdinalIgnoreCase) || 
                    Entity.DataType.Equals("Bit", System.StringComparison.OrdinalIgnoreCase)))
                {
                    return $"{Entity.Address}.{Entity.BitIndex}";
                }
                return Entity.Address;
            }
        }

        /// <summary>
        /// 格式化后的同步转发目标地址。
        /// 格式：设备名[地址.位]。如果未启用同步，则显示 "--"。
        /// </summary>
        public string FormattedSyncTarget
        {
            get
            {
                if (!Entity.IsSyncEnabled || !Entity.SyncTargetAddress.HasValue) return "--";
                string target = $"{SyncTargetDeviceName}[{Entity.SyncTargetAddress}";
                // 如果是位类型，则添加位偏移
                if (Entity.DataType != null && (Entity.DataType.Equals("Bool", System.StringComparison.OrdinalIgnoreCase) || 
                    Entity.DataType.Equals("Bit", System.StringComparison.OrdinalIgnoreCase)))
                {
                    target += $".{Entity.SyncTargetBitIndex}";
                }
                target += "]";
                return target;
            }
        }

        private string _syncTargetDeviceName = "";
        /// <summary>
        /// 同步目标设备的名称。
        /// 由外部加载时匹配 DeviceID 后填入。
        /// </summary>
        public string SyncTargetDeviceName { get => _syncTargetDeviceName; set { _syncTargetDeviceName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedSyncTarget)); } }

        private string _bindingFullPath = "未绑定";
        /// <summary>
        /// 业务绑定的全息路径。
        /// 展示格式：站台名称 > 分组名称 > 对象名称 > 功能描述。
        /// 用于在点表中直观查看该物理点位对应哪个业务对象。
        /// </summary>
        public string BindingFullPath { get => _bindingFullPath; set { _bindingFullPath = value; OnPropertyChanged(); } }

        /// <summary>
        /// 格式化后的模拟量高报限值显示字符串。
        /// 例如 "HH: 80.0"，未配置则显示 "--"。
        /// </summary>
        public string AnalogHighLimitDisplay => Entity.HighLimit.HasValue ? $"HH: {Entity.HighLimit.Value:F1}" : "--";

        /// <summary>
        /// 格式化后的模拟量低报限值显示字符串。
        /// 例如 "LL: 20.0"，未配置则显示 "--"。
        /// </summary>
        public string AnalogLowLimitDisplay => Entity.LowLimit.HasValue ? $"LL: {Entity.LowLimit.Value:F1}" : "--";

        /// <summary>
        /// 判断当前点位是否为模拟量类型。
        /// 用于 UI 控制特定属性（如限值配置区）的显隐。
        /// </summary>
        public bool IsAnalog => Entity.DataType != null && 
                               !Entity.DataType.Equals("Bool", System.StringComparison.OrdinalIgnoreCase) && 
                               !Entity.DataType.Equals("Bit", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 日志分类的友好显示名称。
        /// 动态从全局字典 (PointLogEntity.LogTypeMap) 获取，解耦了数据库 ID 与 UI 显示。
        /// </summary>
        public string LogTypeDisplay
        {
            get
            {
                if (PointLogEntity.LogTypeMap.TryGetValue(Entity.LogTypeId, out var name)) return name;
                return Entity.LogTypeId == 2 ? "报警记录" : "一般记录"; // 保底逻辑
            }
        }
    }
}
