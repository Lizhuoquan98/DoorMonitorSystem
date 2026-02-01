using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Door
{
    /// <summary>门表</summary>
    [Table("Door")]
    public class DoorEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>所属门组ID</summary>
        [Required]
        public int DoorGroupId { get; set; }

        /// <summary>门名称/门号</summary>
        [Required, StringLength(50)]
        public string DoorName { get; set; } = "";

        /// <summary>门类型ID（关联DoorType表）</summary>
        [Required]
        public int DoorTypeId { get; set; }

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>起始字节偏移 (相对基址)</summary>
        public int ByteStartAddr { get; set; }

        /// <summary>数据字节长度/宽度</summary>
        public int ByteLength { get; set; }
    }
}
