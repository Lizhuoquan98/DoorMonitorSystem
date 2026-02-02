using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 屏蔽门组（门域）配置实体类。
    /// 对应数据库 DoorGroup 表，代表一组物理关联的门（如：1号屏蔽门到10号屏蔽门为一个组）。
    /// </summary>
    [Table("DoorGroup")]
    public class DoorGroupEntity
    {
        /// <summary>
        /// 数据库自增主键 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 门组显示名称。
        /// </summary>
        [Required, StringLength(50)]
        public string GroupName { get; set; } = "";

        /// <summary>
        /// 该组在界面上的显示排序序号。
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// 全局唯一标识符 (GUID)。
        /// </summary>
        [StringLength(50)]
        public string KeyId { get; set; }

        /// <summary>
        /// 所属站台的 KeyId。
        /// 维护物理层级关系（站台 -> 门组）。
        /// </summary>
        [StringLength(50)]
        public string StationKeyId { get; set; }

        /// <summary>
        /// 标识门的排序方向。
        /// False = 从左到右顺序（正常），True = 逆向排列，用于适配不同线路的物理安装习惯。
        /// </summary>
        public bool IsReverseOrder { get; set; }
    }
}
