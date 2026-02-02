using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 站台类型定义实体。
    /// 对应数据库 StationType 表，定义站台的结构类型（如：岛式、侧式），用于辅助 UI 布局决策。
    /// </summary>
    [Table("StationType")]
    public class StationTypeEntity
    {
        /// <summary>
        /// 数据库自增 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 类型名称 (如：岛式站台)。
        /// </summary>
        [Required, StringLength(50)]
        public string Name { get; set; } = "";

        /// <summary>
        /// 简短代码 (如：ISLAND, SIDE)。
        /// </summary>
        [StringLength(50)]
        public string Code { get; set; } = "";
    }


}
