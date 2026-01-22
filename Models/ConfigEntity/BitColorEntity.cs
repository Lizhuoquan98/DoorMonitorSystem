using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>颜色字典表</summary>
    [Table("BitColor")]
    public class BitColorEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>颜色名称</summary>
        [Required, StringLength(50)]
        public string ColorName { get; set; } = "";

        /// <summary>颜色值（十六进制）</summary>
        [Required, StringLength(10)]
        public string ColorValue { get; set; } = "";  // #00FF00

        /// <summary>备注</summary>
        [StringLength(200)]
        public string Remark { get; set; } = "";
    }
}
