
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.system
{
    /// <summary>
    /// 系统设备配置表 (对应 devices.json 中的设备对象)
    /// </summary>
    public class SysDeviceEntity
    {
        /// <summary>
        /// 主键 自增
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 逻辑设备ID (对应 ConfigEntity.ID)
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 协议类型 (MODBUS_TCP_CLIENT, S7-1500 etc.)
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// 对时配置 (JSON 序列化存储)
        /// </summary>
        [StringLength(2000)]
        public string TimeSyncJson { get; set; } = "{}";

        /// <summary>
        /// 备注/描述
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 设备通讯参数表 (对应 devices.json 中的 CommParsams)
    /// </summary>
    public class SysDeviceParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 关联 SysDeviceEntity.DeviceId (注意不是主键 Id，是业务 DeviceId，方便查询)
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 参数名 (IP地址, 端口, etc.)
        /// </summary>
        public string ParamName { get; set; }

        /// <summary>
        /// 参数值
        /// </summary>
        public string ParamValue { get; set; }
    }
}
