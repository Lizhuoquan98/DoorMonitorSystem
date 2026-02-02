using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public abstract class ConfigItemBase : NotifyPropertyChanged
    {
        private string _name = "";
        public virtual string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ConfigItemBase> Children { get; set; } = new();
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
    }
}
