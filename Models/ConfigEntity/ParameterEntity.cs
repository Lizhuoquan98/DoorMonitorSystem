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
    }

    /// <summary>
    /// ASD 参数定义模版表 (针对 Sys_ParameterDefines 表)
    /// 定义有哪些参数、单位、提示及 PLC 偏置
    /// </summary>
    [Table("Sys_ParameterDefines")]
    public class ParameterDefineEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>参数标签 (如：最大开门速度)</summary>
        [Required, StringLength(100)]
        public string Label { get; set; } = "";

        /// <summary>单位 (如：mm/s)</summary>
        [StringLength(20)]
        public string Unit { get; set; } = "";

        /// <summary>范围提示 (如：0-10000)</summary>
        [StringLength(100)]
        public string Hint { get; set; } = "";

        /// <summary>参数数据类型 (如: Int16, UInt16, Float, Bool)</summary>
        [StringLength(20)]
        public string DataType { get; set; } = "Int16";

        /// <summary>逻辑绑定键 (如: SpeedOpen, ForceClose) - 用于关联点表 UiBinding</summary>
        [StringLength(50)]
        public string BindingKey { get; set; }

        /// <summary>设备侧写入鉴权值 (如: 1=普通, 2=参数, 3=特权)</summary>
        public int PlcPermissionValue { get; set; } = 1;

        /// <summary>界面显示排序序号</summary>
        public int SortOrder { get; set; }
    }
}
