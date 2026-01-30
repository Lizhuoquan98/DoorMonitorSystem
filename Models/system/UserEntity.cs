using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.system
{
    /// <summary>
    /// 系统用户实体
    /// </summary>
    [Table("Sys_Users")]
    public class UserEntity
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 用户名 (登录账号)
        /// </summary>
        [Column("Username")]
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码 (建议存储哈希值)
        /// </summary>
        [Column("Password")]
        [Required]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 真实姓名
        /// </summary>
        [Column("RealName")]
        [MaxLength(50)]
        public string? RealName { get; set; }

        /// <summary>
        /// 角色 (Admin, Operator, User)
        /// </summary>
        [Column("Role")]
        [MaxLength(50)]
        public string? Role { get; set; }

        /// <summary>
        /// 权限列表 (JSON字符串)
        /// </summary>
        [Column("Permissions")]
        public string? Permissions { get; set; }

        /// <summary>
        /// 账户状态 (1=启用, 0=禁用)
        /// </summary>
        [Column("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("CreateTime")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后登录时间
        /// </summary>
        [Column("LastLoginTime")]
        public DateTime? LastLoginTime { get; set; }
    }
}
