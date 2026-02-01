using System;
using System.Collections.ObjectModel;

namespace DoorMonitorSystem.Models.Ui
{
    public class SelectorNode
    {
        public string Name { get; set; }
        public string FullDescription { get; set; } // Station_Door_Config
        public string ObjectName { get; set; } // New Property for Name Lookup
        public string NodeType { get; set; } // Station, Group, Door, Config, ParamRoot, Param
        public string Role { get; set; } // Read, Write, Auth
        public int Id { get; set; }
        public int ExtendedId { get; set; } // For storing Parent Obj Id
        public int ParentId { get; set; }
        public object Tag { get; set; }
        public ObservableCollection<SelectorNode> Children { get; set; } = new ObservableCollection<SelectorNode>();
    }
}
