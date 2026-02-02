using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Log
{
    /// <summary>日志类型表</summary>
    [Table("LogType")]
    public class LogTypeEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>日志类型名称 (如：普通记录, 报警记录)</summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = "";

        /// <summary>排序序号</summary>
        public int SortOrder { get; set; }
    }
}
