
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.system
{
    /// <summary>
    /// 系统全局配置表 (Key-Value)
    /// 用于替代零散的 JSON 配置文件 (如 DebugConfig.json, NtpConfig.json)
    /// </summary>
    public class SysSettingsEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 配置键 (Unique)
        /// 格式建议: "Category.Key", e.g. "Debug.TraceS7", "Ntp.ServerUrl"
        /// </summary>
        public string SettingKey { get; set; }

        /// <summary>
        /// 配置值
        /// </summary>
        public string SettingValue { get; set; }

        /// <summary>
        /// 分类 (System, Debug, Ntp, etc.)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 描述/备注
        /// </summary>
        public string Description { get; set; }
    }
}
