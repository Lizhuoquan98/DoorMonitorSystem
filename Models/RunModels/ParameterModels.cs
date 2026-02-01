using DoorMonitorSystem.Base;
using System.Collections.Generic;
using DoorMonitorSystem.Models.ConfigEntity;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 参数配置项模型
    /// </summary>
    public class ParameterItem : NotifyPropertyChanged
    {
        private string _label;
        public string Label
        {
            get => _label;
            set { _label = value; OnPropertyChanged(); }
        }

        private string _value = string.Empty;
        /// <summary>设定值</summary>
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        private string _readbackValue = string.Empty;
        /// <summary>读取回来的实际值</summary>
        public string ReadbackValue
        {
            get => _readbackValue;
            set { _readbackValue = value; OnPropertyChanged(); }
        }

        private string _unit;
        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); }
        }

        private string _hint;
        public string Hint
        {
            get => _hint;
            set { _hint = value; OnPropertyChanged(); }
        }

        /// <summary>逻辑绑定键 (关联点表)</summary>
        public string BindingKey { get; set; }

        /// <summary>参数数据类型</summary>
        public string DataType { get; set; }

        /// <summary>设备侧写入鉴权值</summary>
        public int PlcPermissionValue { get; set; }

        private bool _isEditable = true;
        /// <summary>当前用户是否可编辑 (由 ViewModel 计算得出)</summary>
        public bool IsEditable
        {
            get => _isEditable;
            set { _isEditable = value; OnPropertyChanged(); }
        }

        private string _debugInfo;
        /// <summary>调试信息 (显示绑定状态)</summary>
        public string DebugInfo
        {
            get => _debugInfo;
            set { _debugInfo = value; OnPropertyChanged(); }
        }

        /// <summary>关联的点位配置缓存</summary>
        public DevicePointConfigEntity? Config { get; set; }
    }

    /// <summary>
    /// ASD 模型映射配置
    /// </summary>
    public class AsdModelMapping
    {
        public string DisplayName { get; set; }
        /// <summary>下发至 PLC 的物理 ID</summary>
        public int PlcId { get; set; }
    }
}
