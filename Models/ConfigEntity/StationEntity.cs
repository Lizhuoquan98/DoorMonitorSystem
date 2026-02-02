using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 站台基础信息实体类。
    /// 对应数据库 Station 表，定义了地铁站的基本属性、站台布局形式以及层级结构的根节点。
    /// </summary>
    [Table("Station")]
    public class StationEntity
    {
        /// <summary>
        /// 数据库自增主键 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 站台中文名称（例如：南京路站）。
        /// </summary>
        [Required, StringLength(100)]
        public string StationName { get; set; } = "";

        /// <summary>
        /// 站台统一编码。
        /// </summary>
        [StringLength(50)]
        public string StationCode { get; set; } = "";

        /// <summary>
        /// 站台类型标识。
        /// 取值含义：1=岛式站台, 2=侧式站台, 3=三线（双岛式）站台。
        /// 用于在自动生成点位或 UI 布局渲染时作为逻辑判断依据。
        /// </summary>
        public int StationType { get; set; } = 1;

        /// <summary>
        /// 界面展示时的排列序号。
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// 全局唯一标识符 (GUID)。
        /// 用于跨表关联、数据导出导入时的唯一稳定性标识。
        /// </summary>
        [StringLength(50)]
        public string KeyId { get; set; }
    }
}
