using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Group
{
    /// <summary>面板点位配置表</summary>
    [Table("PanelBitConfig")]
    public class PanelBitConfigEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>所属面板类型ID（用于分组管理相同类型的面板点位）</summary>
        [Required]
        public int PanelTypeId { get; set; }

        /// <summary>点位描述</summary>
        [Required, StringLength(100)]
        public string Description { get; set; } = "";

        /// <summary>字节偏移量 (相对于面板的起始地址)</summary>
        public int ByteOffset { get; set; }

        /// <summary>位索引 (0-7)</summary>
        public int BitIndex { get; set; }

        /// <summary>高电平颜色ID</summary>
        [Required]
        public int HighColorId { get; set; }

        /// <summary>低电平颜色ID</summary>
        [Required]
        public int LowColorId { get; set; }

        /// <summary>日志类型 (1=普通记录, 2=报警记录)</summary>
        public int LogTypeId { get; set; } = 1;

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }
    }
}
