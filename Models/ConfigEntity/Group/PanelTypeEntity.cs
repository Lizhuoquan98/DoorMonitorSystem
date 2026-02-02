using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Group
{
    /// <summary>面板类型表</summary>
    [Table("PanelType")]
    public class PanelTypeEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>类型代码</summary>
        [Required, StringLength(10)]
        public string Code { get; set; } = "";

        /// <summary>类型名称</summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = "";
    }
}
