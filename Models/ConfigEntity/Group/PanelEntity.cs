using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Group
{
    /// <summary>面板表</summary>
    [Table("Panel")]
    public class PanelEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>面板名称</summary>
        [Required, StringLength(100)]
        public string PanelName { get; set; } = "";

        /// <summary>标题位置（1=顶部, 2=底部）</summary>
        public int TitlePosition { get; set; } = 2;

        /// <summary>点位布局行数（如 2×3 中的 2）</summary>
        public int LayoutRows { get; set; } = 0;

        /// <summary>点位布局列数（如 2×3 中的 3）</summary>
        public int LayoutColumns { get; set; } = 1;

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>面板类型 (关联 PanelType.KeyId)</summary>
        [StringLength(50)]
        public string PanelTypeKeyId { get; set; }

        /// <summary>起始字节地址 (在组内)</summary>
        public int ByteStartAddr { get; set; } // This property was "起始字节偏移 (相对基址)" in original, but the instruction's context implies it should be here.

        /// <summary>数据字节长度/宽度</summary>
        public int ByteLength { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>关联父级面板组的KeyId</summary>
        [StringLength(50)]
        public string ParentKeyId { get; set; }
    }
}
