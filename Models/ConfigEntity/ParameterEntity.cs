using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// ASD 型号与 PLC ID 映射表 (针对 Sys_AsdModels 表)
    /// </summary>
    [Table("Sys_AsdModels")]
    public class AsdModelMappingEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>显示名称 (如 ASD1)</summary>
        [Required, StringLength(50)]
        public string DisplayName { get; set; } = "";

        /// <summary>对应的 PLC 物理站号 (如 1, 24, 32)</summary>
        public int PlcId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        [StringLength(50)]
        public string KeyId { get; set; }
    }



    /// <summary>
    /// ASD 参数定义模版表 (针对 Sys_ParameterDefines 表)。
    /// 定义系统支持的各类业务参数元数据，包括名称、单位、数据类型及在 PLC 中的存储地址布局。
    /// </summary>
    [Table("Sys_ParameterDefines")]
    public class ParameterDefineEntity
    {
        /// <summary>
        /// 数据库自增 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 全局唯一标识 (GUID)。
        /// </summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>
        /// 参数显示标签 (如：最大开门速度)。
        /// </summary>
        [Required, StringLength(100)]
        public string Label { get; set; } = "";

        /// <summary>
        /// 单位 (如：mm/s, ms)。
        /// </summary>
        [StringLength(20)]
        public string Unit { get; set; } = "";

        /// <summary>
        /// 范围或输入提示 (如：0-10000)。
        /// </summary>
        [StringLength(100)]
        public string Hint { get; set; } = "";

        /// <summary>
        /// 参数数据类型 (如: Int16, UInt16, Float, Bool)。
        /// 决定读取和写入时的字节长度及解析方式。
        /// </summary>
        [StringLength(20)]
        public string DataType { get; set; } = "Int16";

        /// <summary>
        /// 字节偏移量。
        /// 相对于参数块基地址的偏移。
        /// </summary>
        public int ByteOffset { get; set; }

        /// <summary>
        /// 位索引 (0-15)。
        /// 仅当 DataType 为 Bool 时有效，指定字中的具体的 Bit。
        /// </summary>
        public int BitIndex { get; set; }

        /// <summary>
        /// 逻辑绑定键 (Mapping Key)。
        /// 如: SpeedOpen, ForceClose。用于代码中通过 Key 查找特定参数，而不依赖 ID。
        /// </summary>
        [StringLength(50)]
        public string BindingKey { get; set; }

        /// <summary>
        /// PLC 侧权限值。
        /// 写入时可能需要下发的权限等级标识 (如: 1=普通, 2=参数, 3=特权)。
        /// </summary>
        public int PlcPermissionValue { get; set; } = 1;

        /// <summary>
        /// 界面显示排序序号。
        /// </summary>
        public int SortOrder { get; set; }
    }
}
