using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Group;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public class StationConfigItem : ConfigItemBase
    {
        public StationEntity Entity { get; set; }

        public StationConfigItem(StationEntity entity)
        {
            Entity = entity;
            Name = entity.StationName;
        }

        public override string Name
        {
            get => base.Name;
            set
            {
                if (base.Name != value)
                {
                    base.Name = value;
                    Entity.StationName = value;
                    UpdateChildrenNames();
                }
            }
        }

        public void UpdateName() => Name = Entity.StationName;

        public void UpdateChildrenNames()
        {
            foreach (var child in Children)
            {
                if (child is DoorGroupConfigItem dg)
                    dg.Name = this.Name + "门组";
                else if (child is PanelGroupConfigItem pg)
                    pg.Name = this.Name + "面板组";
            }
        }
    }
}
