using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.Ui
{
    /// <summary>
    /// PLC 地址配置项模型。
    /// 用于在批量生成向导中，定义或调整每个 Door/Panel 对象的起始地址和长度。
    /// 此模型支持在 UI (Datagrid) 中直接编辑地址，后续用于批量计算点位地址。
    /// </summary>
    public class AddressConfigItem : NotifyPropertyChanged
    {
        /// <summary>
        /// 对象 ID (Door.Id 或 Panel.Id)。
        /// </summary>
        public int Id { get; set; }

        private string _name;
        /// <summary>
        /// 对象显示名称 (如: 1号屏蔽门)。
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private int _byteStartAddr;
        /// <summary>
        /// 字节起始地址偏移量。
        /// 相对于设备的基地址或块起始地址的偏移。
        /// </summary>
        public int ByteStartAddr
        {
            get => _byteStartAddr;
            set { _byteStartAddr = value; OnPropertyChanged(); }
        }

        private int _byteLength;
        /// <summary>
        /// 占用字节长度。
        /// 用于计算下一个对象的推荐起始地址。
        /// </summary>
        public int ByteLength
        {
            get => _byteLength;
            set { _byteLength = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 对象类型标识 ("Door" 或 "Panel")。
        /// </summary>
        public string Type { get; set; } 
        
        /// <summary>
        /// 排序序号，用于列表显示顺序。
        /// </summary>
        public int SortOrder { get; set; }
    }
}
