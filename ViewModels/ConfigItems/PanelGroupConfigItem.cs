using DoorMonitorSystem.Models.ConfigEntity.Group;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public class PanelGroupConfigItem : ConfigItemBase
    {
        public PanelGroupEntity Entity { get; set; }
        public StationConfigItem Parent { get; set; }

        public PanelGroupConfigItem(PanelGroupEntity entity, StationConfigItem parent)
        {
            Entity = entity;
            Parent = parent;
            // 如果 Entity 名字为空或者默认值，尝试使用标准命名
            if (string.IsNullOrEmpty(entity.GroupName) || entity.GroupName == "新面板组")
                Name = parent.Name + "面板组";
            else
                Name = entity.GroupName;
        }

        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = value;
                Entity.GroupName = value;
            }
        }

        public void UpdateName() => Name = Entity.GroupName;
    }
}
