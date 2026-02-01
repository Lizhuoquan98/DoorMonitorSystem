using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Door
{
    /// <summary>门点位配置表（按门类型定义的模板）</summary>
    [Table("DoorBitConfig")]
    public class DoorBitConfigEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>所属门类型ID</summary>
        [Required]
        public int DoorTypeId { get; set; }

        /// <summary>点位描述</summary>
        [Required, StringLength(100)]
        public string Description { get; set; } = "";

        /// <summary>字节偏移量 (相对于门的起始地址)</summary>
        public int ByteOffset { get; set; }

        /// <summary>位索引 (0-7)</summary>
        public int BitIndex { get; set; }

        /// <summary>点位分类ID（关联 BitCategory 表，用于弹窗分栏显示）</summary>
        public int? CategoryId { get; set; }

        /// <summary>头部显示优先级</summary>
        public int HeaderPriority { get; set; }

        /// <summary>中间图形显示优先级</summary>
        public int ImagePriority { get; set; }

        /// <summary>底部显示优先级</summary>
        public int BottomPriority { get; set; }

        /// <summary>高电平颜色ID（用于弹窗指示灯）</summary>
        [Required]
        public int HighColorId { get; set; }

        /// <summary>低电平颜色ID（用于弹窗指示灯）</summary>
        [Required]
        public int LowColorId { get; set; }

        /// <summary>头部颜色ID</summary>
        public int? HeaderColorId { get; set; }

        /// <summary>底部颜色ID</summary>
        public int? BottomColorId { get; set; }

        /// <summary>图形名称（从全局图形字典获取，图形包含完整颜色信息）</summary>
        [StringLength(50)]
        public string GraphicName { get; set; } = "";

        /// <summary>日志类型 (1=普通记录, 2=报警记录)</summary>
        public int LogTypeId { get; set; } = 1;

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }
    }
}
