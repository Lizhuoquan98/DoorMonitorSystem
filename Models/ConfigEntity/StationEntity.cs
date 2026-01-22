using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>站台表</summary>
    [Table("Station")]
    public class StationEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>站台名称</summary>
        [Required, StringLength(100)]
        public string StationName { get; set; } = "";

        /// <summary>站台编码</summary>
        [StringLength(50)]
        public string StationCode { get; set; } = "";

        /// <summary>站台类型（1=岛式, 2=侧式, 3=三线站台）</summary>
        public int StationType { get; set; } = 1;

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }
    }
}
