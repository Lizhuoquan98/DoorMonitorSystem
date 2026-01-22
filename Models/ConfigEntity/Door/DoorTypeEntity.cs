using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Door
{
    /// <summary>门类型表（滑动门/应急门/端门）</summary>
    [Table("DoorType")]
    public class DoorTypeEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>类型代码</summary>
        [Required, StringLength(10)]
        public string Code { get; set; } = "";  // SlidingDoor / EmergencyDoor / EndDoor

        /// <summary>类型名称</summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = "";

        /// <summary>弹窗点位布局行数（0表示自动）</summary>
        public int PopupLayoutRows { get; set; } = 0;

        /// <summary>弹窗点位布局列数（2表示2列显示）</summary>
        public int PopupLayoutColumns { get; set; } = 2;
    }
}
