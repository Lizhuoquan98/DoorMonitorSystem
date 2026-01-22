using DoorMonitorSystem.Base; 

namespace DoorMonitorSystem.Models.Points
{
    public class DevicePoint : NotifyPropertyChanged
    {
        public int Id { get; set; }
        public int Devid { get; set; }                   // 设备ID
        public int PointId { get; set; }                 // 点位编号 
         public int DBNumber { get; set; }               // DB编号 
        public string Description { get; set; } = "";    // 点位描述
        public int VarType { get; set; }                 // 数据类型
        public int  Address { get; set; }                // 寄存器地址
        public int  BitOffset { get; set; }              // 位地址
        public int  IsAlarm { get; set; }                // 是否报警提示
        public int  RecordLevel { get; set; }            // 是否记录
        public string BitHigh { get; set; } = "";    // Bit 高位状态描述
        public string BitLow{ get; set; } = "";      // Bit 低位状态描述
        public string ModbusAddress { get; set; } = "";  // 关联modbus地址
        public string UiBinding { get; set; } = "";      // 关联UI路径

        private object? _value;                           // 数据值
        public object Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
        public DevicePoint Clone() => (DevicePoint)this.MemberwiseClone();

    }

}
