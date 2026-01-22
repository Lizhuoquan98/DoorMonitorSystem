using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>点位分类表（用于弹窗分栏显示）</summary>
    [Table("BitCategory")]
    public class BitCategoryEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>分类代码（用于程序识别）</summary>
        [Required, StringLength(50)]
        public string Code { get; set; } = "";  // Fault / Alarm / Status

        /// <summary>分类名称（显示在弹窗栏标题）</summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = "";  // 故障 / 报警 / 状态

        /// <summary>图标字符（显示在栏标题前，如 ⚠/🔔/ℹ 等）</summary>
        [StringLength(10)]
        public string? Icon { get; set; }

        /// <summary>背景颜色（十六进制，如 #FF5722）</summary>
        [StringLength(20)]
        public string? BackgroundColor { get; set; }

        /// <summary>前景颜色（十六进制，如 #FFFFFF）</summary>
        [StringLength(20)]
        public string? ForegroundColor { get; set; }

        /// <summary>排序序号（决定弹窗中分栏的显示顺序）</summary>
        public int SortOrder { get; set; }

        /// <summary>点位布局行数（0 表示自动）</summary>
        public int LayoutRows { get; set; } = 0;

        /// <summary>点位布局列数（如 2 表示 2 列显示）</summary>
        public int LayoutColumns { get; set; } = 2;
    }
}
