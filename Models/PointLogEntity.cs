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
        
        public DateTime LogTime { get; set; }

        // --- 辅助显示属性 ---
        [NotMapped]
        public string LogTypeDisplay => LogType == 1 ? "报警" : "状态";
        [NotMapped]
        public string ValDisplay => Val == 1 ? "ON" : "OFF";
    }
}
