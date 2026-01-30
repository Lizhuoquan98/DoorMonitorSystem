using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.system
{
    /// <summary>
    /// 用户操作日志实体
    /// </summary>
    [Table("Sys_OperationLogs")]
    public class OperationLogEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        [Column("LogTime")]
        public DateTime LogTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 操作人用户名
        /// </summary>
        [Column("Username")]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型 (例如: 登录, 退出, 开门, 修改配置)
        /// </summary>
        [Column("OperationType")]
        [MaxLength(50)]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// 详细描述
        /// </summary>
        [Column("Description")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 结果 (成功/失败)
        /// </summary>
        [Column("Result")]
        [MaxLength(20)]
        public string Result { get; set; } = "Success";
    }
}
