using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>门组表</summary>
    [Table("DoorGroup")]
    public class DoorGroupEntity
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
