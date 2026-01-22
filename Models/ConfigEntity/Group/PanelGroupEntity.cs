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

        /// <summary>所属站台ID</summary>
        [Required]
        public int StationId { get; set; }

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }
    }
}
