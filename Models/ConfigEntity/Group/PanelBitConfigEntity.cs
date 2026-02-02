using DoorMonitorSystem.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Group
{
    /// <summary>
    /// 面板点位功能配置模板表。
    /// 对应数据库 PanelBitConfig 表，定义了特定控制面板包含哪些状态指示灯或按钮点位。
    /// 与 DoorBitConfig 类似，作为批量生成点位的模板。
    /// </summary>
    [Table("PanelBitConfig")]
    public class PanelBitConfigEntity : NotifyPropertyChanged
    {
        /// <summary>
        /// 数据库自增 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        private string _keyId;
        /// <summary>
        /// 全局唯一标识 (GUID)。
        /// </summary>
        [StringLength(50)]
        public string KeyId { get => _keyId; set { _keyId = value; OnPropertyChanged(); } }

        private string _panelKeyId;
        /// <summary>
        /// 关联的具体面板实例 KeyId。
        /// 注意：不同于门模板关联的是“门类型”，面板配置往往与特定“面板实例”直接挂钩，或者此处命名有误实际应为 PanelTypeKeyId (需根据业务逻辑确认，暂按变量名解释为面板键)。
        /// </summary>
        [Required, StringLength(50)]
        public string PanelKeyId { get => _panelKeyId; set { _panelKeyId = value; OnPropertyChanged(); } }

        private string _description = "";
        /// <summary>
        /// 点位功能描述 (如：紧急停车按钮)。
        /// </summary>
        [Required, StringLength(100)]
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        private int _byteOffset;
        /// <summary>
        /// 字节偏移量。
        /// 相对于面板基地址的偏移。
        /// </summary>
        public int ByteOffset { get => _byteOffset; set { _byteOffset = value; OnPropertyChanged(); } }

        private int _bitIndex;
        /// <summary>
        /// 位索引 (0-7)。
        /// 仅当 DataType 为 Bool 时有效。
        /// </summary>
        public int BitIndex { get => _bitIndex; set { _bitIndex = value; OnPropertyChanged(); } }

        private string _dataType = "Bool";
        /// <summary>
        /// 数据类型 (如: Bool, Word)。
        /// </summary>
        [StringLength(20)]
        public string DataType { get => _dataType; set { _dataType = value; OnPropertyChanged(); } }

        private int _highColorId;
        /// <summary>
        /// 高电平 (1) 时的指示颜色 ID。
        /// </summary>
        [Required]
        public int HighColorId { get => _highColorId; set { _highColorId = value; OnPropertyChanged(); } }

        private int _lowColorId;
        /// <summary>
        /// 低电平 (0) 时的指示颜色 ID。
        /// </summary>
        [Required]
        public int LowColorId { get => _lowColorId; set { _lowColorId = value; OnPropertyChanged(); } }

        private int _logTypeId = 1;
        /// <summary>
        /// 默认关联的日志类型 ID。
        /// </summary>
        public int LogTypeId { get => _logTypeId; set { _logTypeId = value; OnPropertyChanged(); } }

        private int _sortOrder;
        /// <summary>
        /// 界面列表排序序号。
        /// </summary>
        public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    }
}
