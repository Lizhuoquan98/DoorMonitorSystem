using DoorMonitorSystem.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoorMonitorSystem.Models.ConfigEntity.Door
{
    /// <summary>
    /// 门点位功能配置模板表。
    /// 对应数据库 DoorBitConfig 表，定义了特定类型的门包含哪些状态点位（如：锁紧、隔离、开到位）。
    /// 这些配置是模板，用于批量生成具体的 DevicePointConfig 记录。
    /// </summary>
    [Table("DoorBitConfig")]
    public class DoorBitConfigEntity : NotifyPropertyChanged
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

        private string _doorTypeKeyId;
        /// <summary>
        /// 关联的门类型 KeyId。
        /// 标识该点位模板属于哪种类型的门 (如：滑动门)。
        /// </summary>
        [Required, StringLength(50)]
        public string DoorTypeKeyId { get => _doorTypeKeyId; set { _doorTypeKeyId = value; OnPropertyChanged(); } }

        private string _description = "";
        /// <summary>
        /// 点位功能描述 (如：锁紧状态)。
        /// </summary>
        [Required, StringLength(100)]
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        private int _byteOffset;
        /// <summary>
        /// 字节偏移量。
        /// 相对于该门对象基地址的偏移字节数。
        /// </summary>
        public int ByteOffset { get => _byteOffset; set { _byteOffset = value; OnPropertyChanged(); } }

        private int _bitIndex;
        /// <summary>
        /// 位索引 (0-7)。
        /// 仅当 DataType 为 Bool 时有效，指定在字节中的哪一位。
        /// </summary>
        public int BitIndex { get => _bitIndex; set { _bitIndex = value; OnPropertyChanged(); } }

        private string _dataType = "Bool";
        /// <summary>
        /// 数据类型 (默认 Bool)。
        /// </summary>
        [StringLength(20)]
        public string DataType { get => _dataType; set { _dataType = value; OnPropertyChanged(); } }

        private int? _categoryId;
        /// <summary>
        /// 所属功能分组 ID (保留字段, 用于更细粒度的 UI 分组)。
        /// </summary>
        public int? CategoryId { get => _categoryId; set { _categoryId = value; OnPropertyChanged(); } }

        private int _headerPriority;
        /// <summary>
        /// 顶部标题栏显示优先级。
        /// 数值越大优先级越高，用于决定哪些重要点位显示在门卡片顶部。
        /// </summary>
        public int HeaderPriority { get => _headerPriority; set { _headerPriority = value; OnPropertyChanged(); } }

        private int _imagePriority;
        /// <summary>
        /// 图形化显示优先级。
        /// 用于决定在图形化视图中主要显示哪个状态。
        /// </summary>
        public int ImagePriority { get => _imagePriority; set { _imagePriority = value; OnPropertyChanged(); } }

        private int _bottomPriority;
        /// <summary>
        /// 底部状态栏显示优先级。
        /// 用于决定哪些状态显示在门卡片底部。
        /// </summary>
        public int BottomPriority { get => _bottomPriority; set { _bottomPriority = value; OnPropertyChanged(); } }

        private int _highColorId;
        /// <summary>
        /// 高电平 (1) 时的颜色 ID。
        /// </summary>
        [Required]
        public int HighColorId { get => _highColorId; set { _highColorId = value; OnPropertyChanged(); } }

        private int _lowColorId;
        /// <summary>
        /// 低电平 (0) 时的颜色 ID。
        /// </summary>
        [Required]
        public int LowColorId { get => _lowColorId; set { _lowColorId = value; OnPropertyChanged(); } }

        private int? _headerColorId;
        /// <summary>标题栏高亮颜色 ID (可选)。</summary>
        public int? HeaderColorId { get => _headerColorId; set { _headerColorId = value; OnPropertyChanged(); } }

        private int? _bottomColorId;
        /// <summary>底部栏高亮颜色 ID (可选)。</summary>
        public int? BottomColorId { get => _bottomColorId; set { _bottomColorId = value; OnPropertyChanged(); } }

        private string _graphicName = "";
        /// <summary>
        /// 关联的图标文件名或资源 Key。
        /// </summary>
        [StringLength(50)]
        public string GraphicName { get => _graphicName; set { _graphicName = value; OnPropertyChanged(); } }

        private int _logTypeId = 1;
        /// <summary>
        /// 默认关联的日志类型 ID。
        /// 生成点位时会自动继承此值 (如：故障报警类点位默认为 2)。
        /// </summary>
        public int LogTypeId { get => _logTypeId; set { _logTypeId = value; OnPropertyChanged(); } }

        private int _sortOrder;
        /// <summary>
        /// 界面列表排序序号。
        /// </summary>
        public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }
    }
}
