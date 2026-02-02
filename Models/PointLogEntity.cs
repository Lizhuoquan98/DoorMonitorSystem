using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models
{
    /// <summary>
    /// 点位日志实体渲染模型。
    /// 对应数据库中的 PointLogs 表，用于记录系统运行过程中的所有状态变化、报警及数值记录。
    /// </summary>
    [Table("PointLogs")]
    public class PointLogEntity
    {
        /// <summary>
        /// 自增主键 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }

        /// <summary>
        /// 关联的点位配置表 ID (DevicePointConfig.Id)。
        /// </summary>
        public int PointID { get; set; }

        /// <summary>
        /// 产生日志的设备 ID (ConfigEntity.ID)。
        /// </summary>
        public int DeviceID { get; set; }

        /// <summary>
        /// 物理地址字符串。记录时点的硬件地址，用于追溯。
        /// </summary>
        public string? Address { get; set; }
        
        /// <summary>
        /// 原始数值 (0 或 1)。对于模拟量，此字段存储量化后的整数值。
        /// </summary>
        public int Val { get; set; }
        
        /// <summary>
        /// 日志分类 ID (对应 LogType 表)。
        /// 区分这是一条“一般记录”、“状态记录”还是“报警记录”。
        /// </summary>
        public int LogType { get; set; }
        
        /// <summary>
        /// 日志详细内容。
        /// 支持模板化消息，包含点位名称、状态描述等。
        /// </summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string? Message { get; set; }
        
        /// <summary>
        /// 值文本描述（冗余字段）。
        /// 例如：“触发”、“取消”、“开启”、“关闭”，用于在无需关联配置表的情况下直接显示。
        /// </summary>
        [StringLength(50)]
        public string? ValText { get; set; }

        /// <summary>
        /// 点位所属的大类（分组名称、位置描述等）。
        /// 来自 DevicePointConfig.Category。
        /// </summary>
        [StringLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// 用户名。记录触发该日志时的当前登录操作员。
        /// </summary>
        [StringLength(50)]
        public string? UserName { get; set; }

        /// <summary>
        /// 日志产生的时间戳。
        /// </summary>
        [Column(TypeName = "DATETIME(3)")]
        public DateTime LogTime { get; set; }

        /// <summary>
        /// 全局日志分类名称映射字典。
        /// 静态变量，由 SystemLogViewModel 加载一次后全局共享，用于将 LogType ID 转换为中文名称。
        /// </summary>
        public static System.Collections.Generic.Dictionary<int, string> LogTypeMap { get; set; } = new();

        // --- 辅助显示属性 (NotMapped) ---
        
        /// <summary>
        /// 日志分类的友好中文显示名称。
        /// 内部逻辑：优先查找 LogTypeMap，找不到则根据 ID 2 默认为“报警”。
        /// </summary>
        [NotMapped]
        public string LogTypeDisplay 
        {
            get 
            {
                if (LogTypeMap.TryGetValue(LogType, out var name)) return name;
                return LogType == 2 ? "报警" : "状态"; 
            }
        }
        
        /// <summary>
        /// 状态值的友好显示字符串。
        /// </summary>
        [NotMapped]
        public string ValDisplay => !string.IsNullOrEmpty(ValText) ? ValText : (Val == 1 ? "ON" : "OFF");

        /// <summary>
        /// 列表显示的序号（非数据库字段）。
        /// </summary>
        [NotMapped]
        public int RowIndex { get; set; }

        /// <summary>
        /// 标识是否为模拟量点位。
        /// 对于模拟量日志，UI 显示逻辑可能与开关量有所区别。
        /// </summary>
        [NotMapped]
        public bool IsAnalog { get; set; }
    }
}
