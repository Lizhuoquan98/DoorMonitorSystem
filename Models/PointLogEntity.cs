using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models
{
    /// <summary>
    /// 点位日志实体 (对应数据库 PointLogs 表)
    /// </summary>
    [Table("PointLogs")]
    public class PointLogEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { get; set; }
        public int PointID { get; set; }
        public int DeviceID { get; set; }
        public string? Address { get; set; }
        
        /// <summary>
        /// 值 (0/1)
        /// </summary>
        public int Val { get; set; }
        
        /// <summary>
        /// 日志类型
        /// </summary>
        public int LogType { get; set; }
        
        /// <summary>
        /// 日志内容
        /// </summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string? Message { get; set; }
        
        /// <summary>
        /// 值文本 (如: 恢复/触发) - 冗余存储，提高查询效率
        /// </summary>
        [StringLength(50)]
        public string? ValText { get; set; }

        /// <summary>
        /// 分类 (来自 DevicePointConfig.Category)
        /// </summary>
        [StringLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// 用户名 (记录产生时的操作员)
        /// </summary>
        [StringLength(50)]
        public string? UserName { get; set; }

        [Column(TypeName = "DATETIME(3)")]
        public DateTime LogTime { get; set; }

        // --- 辅助显示属性 ---
        [NotMapped]
        public string LogTypeDisplay => LogType == 2 ? "报警" : "状态";
        
        [NotMapped]
        public string ValDisplay => !string.IsNullOrEmpty(ValText) ? ValText : (Val == 1 ? "ON" : "OFF");

        [NotMapped]
        public int RowIndex { get; set; }

        /// <summary>
        /// 是否为模拟量 (瞬时值)
        /// </summary>
        [NotMapped]
        public bool IsAnalog { get; set; }
    }
}
