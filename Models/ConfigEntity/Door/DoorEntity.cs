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

        /// <summary>门名称/门号</summary>
        [Required, StringLength(50)]
        public string DoorName { get; set; } = "";

        /// <summary>门类型ID（关联DoorType表）</summary>
        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>起始字节偏移 (相对基址)</summary>
        public int ByteStartAddr { get; set; }

        /// <summary>字节长度 (例如 1)</summary>
        public int ByteLength { get; set; } = 1;

        /// <summary>门类型 (关联 DoorType.KeyId)</summary>
        [StringLength(50)]
        public string DoorTypeKeyId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>关联父级门组的KeyId</summary>
        [StringLength(50)]
        public string ParentKeyId { get; set; }
    }
}
