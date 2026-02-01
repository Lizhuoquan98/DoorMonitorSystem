using DoorMonitorSystem.Models.RunModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 设备点位配置表（核心映射表）
    /// 负责将 协议数据(SourceDevice) 映射到 业务对象(BindingDoor/ Panel) 
    /// 并支持 跨设备数据同步(SyncTarget)
    /// </summary>
    [Table("DevicePointConfig")]
    public class DevicePointConfigEntity : NotifyPropertyChanged
    {
        private int _id;
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _category;
        /// <summary>分类 (用于筛选日志等)</summary>
        [StringLength(50)]
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }

        #region 数据源配置 (From)

        private int _sourceDeviceId;
        /// <summary>源设备ID (关联 DeviceConfig)</summary>
        [Required]
        public int SourceDeviceId { get => _sourceDeviceId; set { _sourceDeviceId = value; OnPropertyChanged(); } }

        private string _address = "0";
        /// <summary>寄存器地址 (支持复杂格式，如 "40001", "DB100.10", "M100")</summary>
        [Required, StringLength(50)]
        public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }

        private string _uiBinding;
        /// <summary>UI 绑定键 (对应 ParameterDefineEntity.BindingKey)</summary>
        [StringLength(50)]
        public string UiBinding { get => _uiBinding; set { _uiBinding = value; OnPropertyChanged(); } }

        private string _bindingRole;
        /// <summary>绑定角色 (Read=读取/状态, Write=写入/控制, Auth=鉴权, DoorId=门号选择)</summary>
        [StringLength(20)]
        public string BindingRole { get => _bindingRole; set { _bindingRole = value; OnPropertyChanged(); } }

        private string _dataType = "Word";
        /// <summary>数据类型/长度 (例如 "Word", "Float", "Bool" 或字节长度)</summary>
        [StringLength(20)]
        public string DataType { get => _dataType; set { _dataType = value; OnPropertyChanged(); } }

        private int _functionCode;
        /// <summary>区域/功能码 (Modbus功能码 或 S7区域类型)</summary>
        public int FunctionCode { get => _functionCode; set { _functionCode = value; OnPropertyChanged(); } }

        private int _bitIndex;
        /// <summary>位索引 (0-15, 用于从Word中提取Bit)</summary>
        [Range(0, 15)]
        public int BitIndex { get => _bitIndex; set { _bitIndex = value; OnPropertyChanged(); } }

        #endregion

        #region 业务对象绑定 (To UI)

        private TargetType _targetType;
        /// <summary>目标类型 (1=门, 2=面板)</summary>
        public TargetType TargetType { get => _targetType; set { _targetType = value; OnPropertyChanged(); } }

        private int _targetObjId;
        /// <summary>目标对象ID (DoorId 或 PanelId)</summary>
        public int TargetObjId { get => _targetObjId; set { _targetObjId = value; OnPropertyChanged(); } }

        private int _targetBitConfigId;
        /// <summary>
        /// 绑定的点位配置ID (关联 DoorBitConfig.Id 或 PanelBitConfig.Id)
        /// 用于确定这个点位代表什么意思（如：开门到位、故障等）
        /// </summary>
        public int TargetBitConfigId { get => _targetBitConfigId; set { _targetBitConfigId = value; OnPropertyChanged(); } }

        #endregion

        #region 交互与同步 (Interaction)

        private bool _isSyncEnabled;
        /// <summary>是否开启数据同步/转发</summary>
        public bool IsSyncEnabled { get => _isSyncEnabled; set { _isSyncEnabled = value; OnPropertyChanged(); } }

        private int? _syncTargetDeviceId;
        /// <summary>同步目标设备ID (将读取到的值写入该设备)</summary>
        public int? SyncTargetDeviceId { get => _syncTargetDeviceId; set { _syncTargetDeviceId = value; OnPropertyChanged(); } }

        private ushort? _syncTargetAddress;
        /// <summary>同步写入地址</summary>
        public ushort? SyncTargetAddress { get => _syncTargetAddress; set { _syncTargetAddress = value; OnPropertyChanged(); } }
        
        private int? _syncTargetBitIndex;
        /// <summary>
        /// 同步目标位索引
        /// </summary>
        public int? SyncTargetBitIndex { get => _syncTargetBitIndex; set { _syncTargetBitIndex = value; OnPropertyChanged(); } }
        
        private int _syncMode;
        /// <summary>
        /// 同步值处理模式 (0=直接转发, 1=取反转发)
        /// </summary>
        public int SyncMode { get => _syncMode; set { _syncMode = value; OnPropertyChanged(); } }

        #endregion

        
        #region 日志配置 (Logging)

        private bool _isLogEnabled;
        /// <summary>是否开启日志记录</summary>
        public bool IsLogEnabled { get => _isLogEnabled; set { _isLogEnabled = value; OnPropertyChanged(); } }

        private int _logTypeId = 1;
        /// <summary>日志类型 (1=普通记录, 2=报警记录)</summary>
        public int LogTypeId { get => _logTypeId; set { _logTypeId = value; OnPropertyChanged(); } }

        private int _logTriggerState;
        /// <summary>日志触发状态 (0=False触发, 1=True触发, 2=双向触发)</summary>
        public int LogTriggerState { get => _logTriggerState; set { _logTriggerState = value; OnPropertyChanged(); } }

        private string _logMessage;
        /// <summary>日志内容模板 (为空则使用Description)</summary>
        [StringLength(200)]
        public string LogMessage { get => _logMessage; set { _logMessage = value; OnPropertyChanged(); } }
        
        private double? _logDeadband;
        /// <summary>记录阈值/死区 (仅模拟量，变化超过此值才记录)</summary>
        public double? LogDeadband { get => _logDeadband; set { _logDeadband = value; OnPropertyChanged(); } }

        #endregion

        #region 模拟量报警配置 (Analog Alarm)

        private double? _highLimit;
        /// <summary>高限报警值 (仅模拟量)</summary>
        public double? HighLimit { get => _highLimit; set { _highLimit = value; OnPropertyChanged(); } }

        private double? _lowLimit;
        /// <summary>低限报警值 (仅模拟量)</summary>
        public double? LowLimit { get => _lowLimit; set { _lowLimit = value; OnPropertyChanged(); } }

        #endregion

        #region 开关量状态描述 (Boolean State)

        private string _state0Desc;
        /// <summary>状态0描述 (例如: 停止, 关)</summary>
        [StringLength(50)]
        public string State0Desc { get => _state0Desc; set { _state0Desc = value; OnPropertyChanged(); } }

        private string _state1Desc;
        /// <summary>状态1描述 (例如: 运行, 开)</summary>
        [StringLength(50)]
        public string State1Desc { get => _state1Desc; set { _state1Desc = value; OnPropertyChanged(); } }

        private int? _alarmTargetValue;
        /// <summary>报警目标值 (0或1, 哪个值代表报警)</summary>
        public int? AlarmTargetValue { get => _alarmTargetValue; set { _alarmTargetValue = value; OnPropertyChanged(); } }

        #endregion

        #region Unused or Legacy

        // Keep existing fields
        #endregion

        private string _description;
        /// <summary>备注说明</summary>
        [StringLength(200)]
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        #region Display Properties (Not Mapped)

        private string _targetObjName;
        [NotMapped]
        public string TargetObjName { get => _targetObjName; set { _targetObjName = value; OnPropertyChanged(); } }

        private string _targetBitConfigName;
        [NotMapped]
        public string TargetBitConfigName { get => _targetBitConfigName; set { _targetBitConfigName = value; OnPropertyChanged(); } }

        private string _syncTargetDeviceName;
        [NotMapped]
        public string SyncTargetDeviceName { get => _syncTargetDeviceName; set { _syncTargetDeviceName = value; OnPropertyChanged(); } }

        private int _rowIndex;
        [NotMapped]
        public int RowIndex { get => _rowIndex; set { _rowIndex = value; OnPropertyChanged(); } }

        private object? _lastValue;
        /// <summary>最后一次读取到的实时值缓存 (用于UI参数回显)</summary>
        [NotMapped]
        public object? LastValue { get => _lastValue; set { _lastValue = value; OnPropertyChanged(); } }

        #endregion
    }

    public enum TargetType
    {
        None = 0,
        Door = 1,
        Panel = 2,
        Station = 3
    }
}
