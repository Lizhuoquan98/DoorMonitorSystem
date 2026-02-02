using DoorMonitorSystem.Models.ConfigEntity.Door;
using System.Collections.ObjectModel;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public class DoorConfigItem : ConfigItemBase
    {
        public DoorEntity Entity { get; set; }
        public DoorGroupConfigItem Parent { get; set; }
        public ObservableCollection<DoorBitConfigEntity> BitConfigs { get; set; } = new();

        public DoorConfigItem(DoorEntity entity, DoorGroupConfigItem parent)
        {
            Entity = entity;
            Parent = parent;
            Name = entity.DoorName;
        }

        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = value;
                Entity.DoorName = value;
            }
        }

        public void UpdateName() => Name = Entity.DoorName;
    }
}
