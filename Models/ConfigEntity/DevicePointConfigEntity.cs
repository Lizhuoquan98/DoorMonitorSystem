using DoorMonitorSystem.Models.RunModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 设备点位配置实体 (底层数据库映射类)。
    /// 对应数据库中的 DevicePointConfig 表，存储点位的物理采集配置（Modbus/S7 地址）、业务对象绑定（关联哪扇门）、数据转发同步规则以及报警/日志触发逻辑。
    /// 所有 UI 展现层所需的复合字段（如格式化地址）应放在展示模型中实现，此处保持实体纯净。
    /// </summary>
    [Table("DevicePointConfig")]
    public class DevicePointConfigEntity : NotifyPropertyChanged
    {
        private int _id;
        /// <summary>
        /// 数据库自增主键 ID。
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _category;
        /// <summary>
        /// 点位所属的大分类。
        /// 用于在配置界面进行批量过滤显示，或者作为日志导出的分类标识（如：1号屏蔽门）。
        /// </summary>
        [StringLength(50)]
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        #region 数据源配置 (采集来源)

        private int _sourceDeviceId;
        /// <summary>
        /// 采集源设备 ID。
        /// 关联通信配置表 (DeviceConfig) 的主键，指定该点位从哪台 PLC 或控制器读取。
        /// </summary>
        [Required]
        public int SourceDeviceId { get => _sourceDeviceId; set { _sourceDeviceId = value; OnPropertyChanged(); } }

        private string _address = "0";
        /// <summary>
        /// 寄存器物理地址。
        /// 取决于通讯驱动类型，例如：Modbus 的 40001，S7 的 100（偏移），DB10.DBW0 等。
        /// </summary>
        [Required, StringLength(50)]
        public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }

        private string _uiBinding;
        /// <summary>
        /// 前端 UI 绑定键名。
        /// 全局唯一的 Key，前端图形界面根据此 Key 订阅实时变化的数据。
        /// </summary>
        [StringLength(50)]
        public string UiBinding { get => _uiBinding; set { _uiBinding = value; OnPropertyChanged(); } }

        private string _bindingRole;
        /// <summary>
        /// 数据绑定角色。
        /// 标识该点位是“只读”数据还是“可下发”指令。常用取值：Read, Write, AuthRow（行授权开关）。
        /// </summary>
        [StringLength(20)]
        public string BindingRole { get => _bindingRole; set { _bindingRole = value; OnPropertyChanged(); } }

        private string _dataType = "Word";
        /// <summary>
        /// 原始数据类型。
        /// 可选：Bool, Bit (位), Word (16位整数), DWord (32位整数), Float (32位浮点数)等。
        /// 通讯引擎根据此类型决定读取的字节长度和解析方式。
        /// </summary>
        [StringLength(20)]
        public string DataType { get => _dataType; set { _dataType = value; OnPropertyChanged(); } }

        private int _functionCode;
        /// <summary>
        /// 区域/功能码。
        /// 对于 Modbus 指功能码（0x03, 0x01 等）；对于 S7 指 DB 块编号或内存区类型。
        /// </summary>
        public int FunctionCode { get => _functionCode; set { _functionCode = value; OnPropertyChanged(); } }

        private int _bitIndex;
        /// <summary>
        /// 位偏移索引 (0-15)。
        /// 当 DataType 为 Word 时，可通过此索引提取该字中的特定 bit。
        /// </summary>
        [Range(0, 15)]
        public int BitIndex { get => _bitIndex; set { _bitIndex = value; OnPropertyChanged(); } }

        #endregion

        #region 业务对象绑定 (关联关系)

        private TargetType _targetType;
        /// <summary>
        /// 绑定的业务对象类型。
        /// 0=无关联, 1=屏蔽门 (Door), 2=控制面板 (Panel), 3=站台参数 (Station)。
        /// </summary>
        public TargetType TargetType { get => _targetType; set { _targetType = value; OnPropertyChanged(); } }

        private string _targetKeyId;
        /// <summary>
        /// 目标业务对象的实例全局 ID (GUID)。
        /// 关联到具体哪一扇门或哪块面板。
        /// </summary>
        [StringLength(50)]
        public string TargetKeyId { get => _targetKeyId; set { _targetKeyId = value; OnPropertyChanged(); } }

        private string _targetBitConfigKeyId;
        /// <summary>
        /// 业务功能的定义配置 ID。
        /// 例如指明该点位对应的是这扇门的“锁紧状态”还是“行程终点信号”。
        /// </summary>
        [StringLength(50)]
        public string TargetBitConfigKeyId { get => _targetBitConfigKeyId; set { _targetBitConfigKeyId = value; OnPropertyChanged(); } }

        #endregion

        #region 数据转发同步 (Sync)

        private bool _isSyncEnabled;
        /// <summary>
        /// 是否启用跨设备同步转发。
        /// 开启后，如果本采集点数据变化，系统会自动将值下发到指定的目标设备地址中。
        /// </summary>
        public bool IsSyncEnabled { get => _isSyncEnabled; set { _isSyncEnabled = value; OnPropertyChanged(); } }

        private int? _syncTargetDeviceId;
        /// <summary>
        /// 同步下发的目标设备 ID。
        /// </summary>
        public int? SyncTargetDeviceId { get => _syncTargetDeviceId; set { _syncTargetDeviceId = value; OnPropertyChanged(); } }

        private ushort? _syncTargetAddress;
        /// <summary>
        /// 同步下发的目标寄存器物理地址。
        /// </summary>
        public ushort? SyncTargetAddress { get => _syncTargetAddress; set { _syncTargetAddress = value; OnPropertyChanged(); } }
        
        private int? _syncTargetBitIndex;
        /// <summary>
        /// 同步下发的目标寄存器位索引。
        /// </summary>
        public int? SyncTargetBitIndex { get => _syncTargetBitIndex; set { _syncTargetBitIndex = value; OnPropertyChanged(); } }
        
        private int _syncMode;
        /// <summary>
        /// 同步模式。
        /// 0=直接转发（原始值下发），1=取反转发（0变1, 1变0后下发）。
        /// </summary>
        public int SyncMode { get => _syncMode; set { _syncMode = value; OnPropertyChanged(); } }

        #endregion

        #region 日志记录配置 (Logging)

        private bool _isLogEnabled;
        /// <summary>
        /// 是否开启该点位的历史变化日志记录。
        /// </summary>
        public bool IsLogEnabled { get => _isLogEnabled; set { _isLogEnabled = value; OnPropertyChanged(); } }

        private int _logTypeId = 1;
        /// <summary>
        /// 记录日志分类 ID。
        /// 映射到 LogType 表，区分记录是“状态信息”还是“故障报警”。
        /// </summary>
        public int LogTypeId { get => _logTypeId; set { _logTypeId = value; OnPropertyChanged(); } }

        private int _logTriggerState = 2;
        /// <summary>
        /// 日志写入触发策略。
        /// 0=仅在 1->0 恢复时记录，1=仅在 0->1 产生时记录，2=任何变化均记录。
        /// </summary>
        public int LogTriggerState { get => _logTriggerState; set { _logTriggerState = value; OnPropertyChanged(); } }

        private string _logMessage;
        /// <summary>
        /// 自定义日志文本模板。
        /// 支持通过占位符替换点位描述和值，用于导出生成更具可读性的中文日志。
        /// </summary>
        [StringLength(200)]
        public string LogMessage { get => _logMessage; set { _logMessage = value; OnPropertyChanged(); } }
        
        private double? _logDeadband;
        /// <summary>
        /// 模拟量数值记录死区。
        /// 仅当数值变化幅度超过此值时才写入一次历史库，防止数据过于频繁。
        /// </summary>
        public double? LogDeadband { get => _logDeadband; set { _logDeadband = value; OnPropertyChanged(); } }

        #endregion

        #region 监控与报警描述 (Monitor)

        private double? _highLimit;
        /// <summary>
        /// 模拟量的高限报警阈值（HH 等级）。
        /// </summary>
        public double? HighLimit { get => _highLimit; set { _highLimit = value; OnPropertyChanged(); } }

        private double? _lowLimit;
        /// <summary>
        /// 模拟量的低限报警阈值（LL 等级）。
        /// </summary>
        public double? LowLimit { get => _lowLimit; set { _lowLimit = value; OnPropertyChanged(); } }

        private string _state0Desc;
        /// <summary>
        /// 开关量值为 0 时的文本描述（如：“解锁”、“关闭”、“正常”）。
        /// </summary>
        [StringLength(50)]
        public string State0Desc { get => _state0Desc; set { _state0Desc = value; OnPropertyChanged(); } }

        private string _state1Desc;
        /// <summary>
        /// 开关量值为 1 时的文本描述（如：“锁定”、“打开”、“故障”）。
        /// </summary>
        [StringLength(50)]
        public string State1Desc { get => _state1Desc; set { _state1Desc = value; OnPropertyChanged(); } }

        private int? _alarmTargetValue;
        /// <summary>
        /// 报警目标电平。
        /// 指定 0 或 1 为报警态，界面会据此高亮显示。
        /// </summary>
        public int? AlarmTargetValue { get => _alarmTargetValue; set { _alarmTargetValue = value; OnPropertyChanged(); } }

        #endregion

        private string _description;
        /// <summary>
        /// 点位的注释或详细说明。
        /// 一般填写该物理信号的实际物理含义。
        /// </summary>
        [StringLength(200)]
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        private object _lastValue;
        /// <summary>
        /// 运行时最后一个采集值缓存 (NotMapped)。
        /// 仅用于通讯引擎判断数据变化和界面实时展示，不存入数据库。
        /// </summary>
        [NotMapped]
        public object LastValue { get => _lastValue; set { _lastValue = value; OnPropertyChanged(); } }
    }

    public enum TargetType
    {
        None = 0,
        Door = 1,
        Panel = 2,
        Station = 3
    }
}
