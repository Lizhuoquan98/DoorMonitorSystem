using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.Ui
{
    public class AddressConfigItem : NotifyPropertyChanged
    {
        public int Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private int _byteStartAddr;
        public int ByteStartAddr
        {
            get => _byteStartAddr;
            set { _byteStartAddr = value; OnPropertyChanged(); }
        }

        private int _byteLength;
        public int ByteLength
        {
            get => _byteLength;
            set { _byteLength = value; OnPropertyChanged(); }
        }

        public string Type { get; set; } // "Door" or "Panel"
        public int SortOrder { get; set; }
    }
}
