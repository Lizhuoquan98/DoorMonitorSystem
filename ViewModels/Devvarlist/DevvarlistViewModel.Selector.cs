using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Models.Ui;
using Base;
using DoorMonitorSystem.Base;
using Communicationlib.config;
using ConfigEntity = Communicationlib.config.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models.ConfigEntity.Log;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 点位配置视图模型 - 选择器模块
    /// </summary>
    public partial class DevvarlistViewModel
    {
        #region 逻辑绑定树形选择器 (Selector)

        /// <summary>
        /// 用于在 UI 树形控件中显示的根节点集合 (对齐 XAML 的 SelectorTree)。
        /// </summary>
        public ObservableCollection<UiSelectorNode> SelectorTree { get; set; } = new ObservableCollection<UiSelectorNode>();

        private UiSelectorNode _selectedTreeNode;
        /// <summary>
        /// 用户在树形结构中当前选中的节点。
        /// </summary>
        public UiSelectorNode SelectedTreeNode
        {
            get => _selectedTreeNode;
            set { _selectedTreeNode = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 加载树指令。
        /// </summary>
        public ICommand LoadTreeCommand => new RelayCommand(obj => LoadSelectorNodes());

        /// <summary>
        /// 节点选择指令 (对齐 XAML)。
        /// </summary>
        public ICommand SelectNodeCommand => new RelayCommand(obj =>
        {
            if (obj is UiSelectorNode node)
            {
                ApplyBinding(node);
                IsPopupOpen = false;
            }
        });

        /// <summary>
        /// 逻辑绑定关联命令 (旧版或备用)。
        /// </summary>
        public ICommand BindPointCommand => new RelayCommand(obj =>
        {
            if (SelectedTreeNode == null) return;
            ApplyBinding(SelectedTreeNode);
        });

        /// <summary>
        /// 应用绑定逻辑。
        /// </summary>
        private void ApplyBinding(UiSelectorNode node)
        {
            if (NewPoint == null || node == null) return;

            switch (node.NodeType)
            {
                case "DoorBit":
                case "Config": // 兼容 XAML 中的 NodeType
                    NewPoint.TargetType = TargetType.Door;
                    NewPoint.TargetKeyId = node.KeyId;
                    NewPoint.TargetBitConfigKeyId = node.BitConfigKeyId;
                    NewPoint.UiBinding = node.BindingKey;
                    NewPoint.BindingRole = "Read";
                    NewPoint.Category = node.Category;
                    NewPoint.Description = $"{(string.IsNullOrEmpty(node.ParentName) ? "" : node.ParentName + "_")}{node.Name}";
                    break;
                
                case "PanelBit":
                    NewPoint.TargetType = TargetType.Panel;
                    NewPoint.TargetKeyId = node.KeyId;
                    NewPoint.TargetBitConfigKeyId = node.BitConfigKeyId;
                    NewPoint.UiBinding = node.BindingKey;
                    NewPoint.BindingRole = "Read";
                    NewPoint.Category = node.Category;
                    NewPoint.Description = $"{(string.IsNullOrEmpty(node.ParentName) ? "" : node.ParentName + "_")}{node.Name}";
                    break;
             
                case "ParamNode":
                case "Param": // 兼容 XAML 中的 NodeType
                    NewPoint.TargetType = TargetType.Station;
                    NewPoint.TargetKeyId = node.KeyId;
                    NewPoint.UiBinding = node.BindingKey;
                    NewPoint.BindingRole = node.BindingRole;
                    NewPoint.Category = node.Category ?? "系统";
                    NewPoint.Description = $"参数: {node.ParentName} [{node.BindingKey}]";
                    break;
            }

            OnPropertyChanged(nameof(NewPoint));
            NewPointBindingPath = node.FullDescription; // 立即同步显示全路径，无需等待后台查询
        }

        /// <summary>
        /// 初始化并加载绑定器树。
        /// 优化后：去掉虚拟分类层级，自动精简名称。
        /// </summary>
        public void LoadSelectorNodes()
        {
            SelectorTree.Clear();
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();

                var stations = db.FindAll<StationEntity>();
                var doors = db.FindAll<DoorEntity>();
                var doorGroups = db.FindAll<DoorGroupEntity>();
                var doorConfigs = db.FindAll<DoorBitConfigEntity>();

                var panels = db.FindAll<PanelEntity>();
                var panelGroups = db.FindAll<PanelGroupEntity>();
                var panelConfigs = db.FindAll<PanelBitConfigEntity>();

                var paramDefines = db.FindAll<ParameterDefineEntity>();

                foreach (var station in stations)
                {
                    // 使用重命名后的 UiSelectorNode，彻底杜绝命名冲突引发的 IsExpanded 编译错误。
                    var stationRoot = new UiSelectorNode { Name = station.StationName, NodeType = "Station", KeyId = station.KeyId, IsExpanded = true };

                    // 1. 门组 (直接挂在站台下)
                    var sGroups = doorGroups.Where(g => g.StationKeyId == station.KeyId).OrderBy(g => g.SortOrder).ToList();
                    foreach (var group in sGroups)
                    {
                        string gName = group.GroupName;
                        // 仅去掉站台前缀和常见分隔符，不再尝试切掉“门”字。
                        if (!string.IsNullOrEmpty(station.StationName) && gName.StartsWith(station.StationName))
                            gName = gName.Substring(station.StationName.Length).TrimStart(' ', '-', '_', '(', ')');
                        
                        // 如果剩下的是空的，再给个保底名称
                        if (string.IsNullOrEmpty(gName)) gName = "门系统组";

                        var groupNode = new UiSelectorNode { Name = gName, NodeType = "Group" };
                        var gDoors = doors.Where(d => d.ParentKeyId == group.KeyId).OrderBy(d => d.DoorName).ToList();
                        foreach (var door in gDoors)
                        {
                            var doorNode = new UiSelectorNode { Name = door.DoorName, NodeType = "Door", KeyId = door.KeyId };
                            foreach (var cfg in doorConfigs.Where(c => c.DoorTypeKeyId == door.DoorTypeKeyId).OrderBy(c => c.SortOrder))
                            {
                                doorNode.Children.Add(new UiSelectorNode
                                {
                                    Name = cfg.Description,
                                    NodeType = "Config",
                                    KeyId = door.KeyId,
                                    BitConfigKeyId = cfg.KeyId,
                                    ParentName = door.DoorName,
                                    Category = door.DoorName, // 设置分类为门名称，方便在点位列表中归类
                                    BindingKey = $"{door.KeyId}_{cfg.KeyId}",
                                    FullDescription = $"{station.StationName} > {group.GroupName} > {door.DoorName} > {cfg.Description}"
                                });
                            }
                            groupNode.Children.Add(doorNode);
                        }
                        stationRoot.Children.Add(groupNode);
                    }

                    // 2. 面板组 (直接挂在站台下)
                    var spGroups = panelGroups.Where(g => g.StationKeyId == station.KeyId).OrderBy(g => g.SortOrder).ToList();
                    foreach (var group in spGroups)
                    {
                        string gName = group.GroupName;
                        if (!string.IsNullOrEmpty(station.StationName) && gName.StartsWith(station.StationName))
                            gName = gName.Substring(station.StationName.Length).TrimStart(' ', '-', '_', '(', ')');

                        if (string.IsNullOrEmpty(gName)) gName = "操作面板组";

                        var groupNode = new UiSelectorNode { Name = gName, NodeType = "Group" };
                        var gPanels = panels.Where(p => p.ParentKeyId == group.KeyId).OrderBy(p => p.PanelName).ToList();
                        foreach (var panel in gPanels)
                        {
                            var panelNode = new UiSelectorNode { Name = panel.PanelName, NodeType = "Panel", KeyId = panel.KeyId };
                            foreach (var cfg in panelConfigs.Where(c => c.PanelKeyId == panel.KeyId).OrderBy(c => c.SortOrder))
                            {
                                panelNode.Children.Add(new UiSelectorNode
                                {
                                    Name = cfg.Description,
                                    NodeType = "Config",
                                    KeyId = panel.KeyId,
                                    BitConfigKeyId = cfg.KeyId,
                                    ParentName = panel.PanelName,
                                    Category = panel.PanelName, // 设置分类为面板名称
                                    BindingKey = $"{panel.KeyId}_{cfg.KeyId}",
                                    FullDescription = $"{station.StationName} > {group.GroupName} > {panel.PanelName} > {cfg.Description}"
                                });
                            }
                            groupNode.Children.Add(panelNode);
                        }
                        stationRoot.Children.Add(groupNode);
                    }

                    // 3. 系统控制项 (如门号下发、读写触发)
                    var controlGroup = new UiSelectorNode { Name = "系统控制", NodeType = "Group" };
                    controlGroup.Children.Add(new UiSelectorNode { 
                        Name = "目标门号下发 (Sys_DoorId)", 
                        NodeType = "Param", 
                        KeyId = station.KeyId, 
                        BindingKey = "Sys_DoorId", 
                        BindingRole = "Write", 
                        Category = "系统控制",
                        FullDescription = $"{station.StationName} > 系统控制 > 门号下发"
                    });
                    controlGroup.Children.Add(new UiSelectorNode { 
                        Name = "参数读取触发 (Sys_ReadTrigger)", 
                        NodeType = "Param", 
                        KeyId = station.KeyId, 
                        BindingKey = "Sys_ReadTrigger", 
                        BindingRole = "Write", 
                        Category = "系统控制",
                        FullDescription = $"{station.StationName} > 系统控制 > 读取触发"
                    });
                    controlGroup.Children.Add(new UiSelectorNode { 
                        Name = "参数写入触发 (Sys_WriteTrigger)", 
                        NodeType = "Param", 
                        KeyId = station.KeyId, 
                        BindingKey = "Sys_WriteTrigger", 
                        BindingRole = "Write", 
                        Category = "系统控制",
                        FullDescription = $"{station.StationName} > 系统控制 > 写入触发"
                    });
                    stationRoot.Children.Add(controlGroup);

                    // 4. 系统参数
                    if (paramDefines.Count > 0)
                    {
                        var paramGroup = new UiSelectorNode { Name = "站台参数模板", NodeType = "Group" };
                        foreach (var p in paramDefines.OrderBy(x => x.SortOrder))
                        {
                            var pNode = new UiSelectorNode 
                            { 
                                Name = p.Label, 
                                NodeType = "Group", 
                                KeyId = station.KeyId,
                                Category = "站台参数",
                                FullDescription = $"参数: {p.Label} [{p.BindingKey}]"
                            };

                            pNode.Children.Add(CreateParamNode("参数写入 (Write)", p.BindingKey, "Write", p.Label, station.KeyId, "站台参数"));
                            pNode.Children.Add(CreateParamNode("参数读取 (Read)", p.BindingKey, "Read", p.Label, station.KeyId, "站台参数"));
                            pNode.Children.Add(CreateParamNode("鉴权操作 (Auth)", p.BindingKey, "Auth", p.Label, station.KeyId, "站台参数"));

                            paramGroup.Children.Add(pNode);
                        }
                        stationRoot.Children.Add(paramGroup);
                    }

                    SelectorTree.Add(stationRoot);
                }
            }
            catch (Exception ex) { LogHelper.Error("加载绑定选择器失败", ex); }
        }

        private UiSelectorNode CreateParamNode(string name, string baseKey, string role, string parentLabel, string keyId, string category)
        {
            return new UiSelectorNode
            {
                Name = name,
                NodeType = "Param",
                KeyId = keyId,
                BindingKey = baseKey,
                BindingRole = role,
                ParentName = parentLabel,
                Category = category,
                FullDescription = $"参数绑定: {parentLabel} - {role}"
            };
        }

        #endregion
    }
}
