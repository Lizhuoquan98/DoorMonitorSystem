using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Group
{
    /// <summary>面板组表</summary>
    [Table("PanelGroup")]
    public class PanelGroupEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>面板组名称</summary>
        [Required, StringLength(50)]
        public string GroupName { get; set; } = "";

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>关联站台的KeyId (结构稳定性)</summary>
        [StringLength(50)]
        public string StationKeyId { get; set; }
    }
}
