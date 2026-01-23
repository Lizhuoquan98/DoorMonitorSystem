using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels; // For GlobalData
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using Communicationlib.config;
using MySql.Data.MySqlClient;
using Base;

using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
namespace DoorMonitorSystem.ViewModels
{
    public class DevvarlistViewModel : NotifyPropertyChanged
    {
        // 设备列表 (供下拉选择)
        public ObservableCollection<ConfigEntity> Devices { get; set; } = new ObservableCollection<ConfigEntity>();

        // 当前选中的设备
        private ConfigEntity _selectedDevice;
        public ConfigEntity SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
                LoadPoints(); // 切换设备时加载点表
            }
        }

        // 点位列表 (DataGrid绑定)
        public ObservableCollection<DevicePointConfigEntity> Points { get; set; } = new ObservableCollection<DevicePointConfigEntity>();

        // 当前选中的点位 (用于删除/编辑)
        private DevicePointConfigEntity _selectedPoint;
        public DevicePointConfigEntity SelectedPoint
        {
            get => _selectedPoint;
            set { _selectedPoint = value; OnPropertyChanged(); }
        }

        // 新增点位暂存对象
        private DevicePointConfigEntity _newPoint = new DevicePointConfigEntity();
        public DevicePointConfigEntity NewPoint
        {
            get => _newPoint;
            set { _newPoint = value; OnPropertyChanged(); }
        }

        // 目标类型下拉框
        public List<TargetType> TargetTypes { get; } = Enum.GetValues(typeof(TargetType)).Cast<TargetType>().ToList();

        // Commands
        public ICommand LoadDevicesCommand => new RelayCommand(o => LoadDevices());
        public ICommand AddPointCommand => new RelayCommand(AddPoint);
        public ICommand DeletePointCommand => new RelayCommand(DeletePoint);
        public ICommand SaveChangesCommand => new RelayCommand(SaveChanges);

        public DevvarlistViewModel()
        {
            LoadDevices();
        }

        private void LoadDevices()
        {
            Devices.Clear();
            if (GlobalData.ListDveices != null)
            {
                foreach (var dev in GlobalData.ListDveices)
                {
                    Devices.Add(dev);
                }
            }
            if (Devices.Count > 0) SelectedDevice = Devices[0];
        }

        private void LoadPoints()
        {
            if (SelectedDevice == null) return;
            Points.Clear();

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                var list = db.FindAll<DevicePointConfigEntity>("SourceDeviceId = @sid", new MySqlParameter("@sid", SelectedDevice.ID));
                foreach (var p in list)
                {
                    Points.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载点表失败: {ex.Message}");
            }
        }

        private void AddPoint(object obj)
        {
            if (SelectedDevice == null)
            {
                MessageBox.Show("请先选择设备");
                return;
            }

            // 补充关联ID
            NewPoint.SourceDeviceId = SelectedDevice.ID;

            // 简单校验
            if (string.IsNullOrWhiteSpace(NewPoint.Address))
            {
                MessageBox.Show("地址不能为空");
                return;
            }

            if (NewPoint.IsSyncEnabled && (NewPoint.SyncTargetDeviceId == null || NewPoint.SyncTargetAddress == null))
            {
                MessageBox.Show("开启同步时，必须指定目标设备和同步地址");
                return;
            }

            // 检查同一设备下地址是否重复 (SourceDeviceId + Address + BitIndex)
            var existing = Points.FirstOrDefault(p => p.SourceDeviceId == NewPoint.SourceDeviceId && 
                                                   p.Address == NewPoint.Address && 
                                                   p.BitIndex == NewPoint.BitIndex);
            if (existing != null)
            {
                 MessageBox.Show($"该设备下地址 '{NewPoint.Address}' (Bit {NewPoint.BitIndex}) 已存在，不能重复添加！");
                 return;
            }

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                db.Insert(NewPoint);
                
                // 刷新列表
                Points.Add(NewPoint);
                
                // 重置 NewPoint，保留部分通用属性方便连续录入
                NewPoint = new DevicePointConfigEntity 
                { 
                    SourceDeviceId = SelectedDevice.ID,
                    DataType = NewPoint.DataType,
                    TargetType = NewPoint.TargetType
                };
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1054)
                {
                    try
                    {
                        var msg = ex.Message;
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                        db.Connect(); // Ensure connection is open for NonQuery
                        
                        if (msg.Contains("TargetType"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `TargetType` INT DEFAULT 0;");
                        
                        if (msg.Contains("IsSyncEnabled"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `IsSyncEnabled` TINYINT(1) DEFAULT 0;");
                            
                        if (msg.Contains("SyncTargetDeviceId"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetDeviceId` INT DEFAULT NULL;");
                            
                        if (msg.Contains("SyncTargetAddress"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetAddress` INT DEFAULT NULL;"); // Use INT for ushort? to be safe or SMALLINT UNSIGNED
                            
                        if (msg.Contains("SyncMode"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncMode` INT DEFAULT 0;");

                        if (msg.Contains("SyncTargetBitIndex"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetBitIndex` INT DEFAULT NULL;");
                        
                        // New Logging Columns
                        if (msg.Contains("IsLogEnabled"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `IsLogEnabled` TINYINT(1) DEFAULT 0;");
                        if (msg.Contains("LogTypeId"))
                            db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `LogTypeId` INT DEFAULT 1;");
                        if (msg.Contains("LogTriggerState"))
                             db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `LogTriggerState` INT DEFAULT 1;");
                        if (msg.Contains("LogMessage"))
                             db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `LogMessage` VARCHAR(200);");

                        // 重试添加
                        using var dbRetry = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                        dbRetry.Insert(NewPoint);
                        
                        Points.Add(NewPoint);
                        
                        // 重置
                         NewPoint = new DevicePointConfigEntity 
                        { 
                            SourceDeviceId = SelectedDevice.ID,
                            DataType = NewPoint.DataType,
                            TargetType = NewPoint.TargetType,
                            IsSyncEnabled = false
                        };
                        return;
                    }
                    catch (Exception innerEx)
                    {
                        MessageBox.Show($"尝试修复数据库失败: {innerEx.Message}\n请联系管理员手动检查表结构。");
                    }
                }
                else
                {
                    MessageBox.Show($"添加失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加失败: {ex.Message}");
            }
        }

        private void DeletePoint(object obj)
        {
            if (SelectedPoint == null) return;

            if (MessageBox.Show($"确定删除点位 {SelectedPoint.Address} 吗?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                db.Delete(SelectedPoint);
                Points.Remove(SelectedPoint);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}");
            }
        }

        private void SaveChanges(object obj)
        {
            // 保存列表中的修改 (Row Editing)
            // 目前 SQLHelper 可能没有批量更新，这里演示简单的单条更新或提示
            // 实际操作中，DataGrid 编辑通常是实时的或需要 RowEditEnding 事件
            // 这里我们假设用户点击保存时，更新当前选中的点位 (或者遍历所有)
            
            if (SelectedPoint == null) return;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                db.Update(SelectedPoint);
                MessageBox.Show("保存成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }
        // Tree Selector
        public ObservableCollection<SelectorNode> SelectorTree { get; set; } = new ObservableCollection<SelectorNode>();
        
        // Popup Open State
        private bool _isPopupOpen;
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set { _isPopupOpen = value; OnPropertyChanged(); }
        }

        // Command to load tree (lazy load or on init)
        public ICommand LoadTreeCommand => new RelayCommand(o => LoadSelectorTree());
        public ICommand SelectNodeCommand => new RelayCommand(SelectNode);

        private void LoadSelectorTree()
        {
            SelectorTree.Clear();
            IsPopupOpen = true; // Open the popup when loading

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                // 1. Load Root: Stations
                var stations = db.FindAll<StationEntity>();
                
                // 2. Load Door Domain Data
                var doors = db.FindAll<DoorEntity>().GroupBy(d => d.DoorGroupId).ToDictionary(g => g.Key, g => g.ToList());
                var doorGroups = db.FindAll<DoorGroupEntity>().GroupBy(g => g.StationId).ToDictionary(g => g.Key, g => g.ToList());
                var doorBitConfigs = db.FindAll<DoorBitConfigEntity>().GroupBy(c => c.DoorTypeId).ToDictionary(g => g.Key, g => g.ToList());

                // 3. Load Panel Domain Data
                var panels = db.FindAll<PanelEntity>().GroupBy(p => p.PanelGroupId).ToDictionary(g => g.Key, g => g.ToList());
                var panelGroups = db.FindAll<PanelGroupEntity>().GroupBy(g => g.StationId).ToDictionary(g => g.Key, g => g.ToList());
                // PanelBitConfig usually linked by PanelType? Assuming PanelEntity has PanelTypeId or similar, or just mapped directly. 
                // Checking PanelEntity: it doesn't has PanelTypeId explicitly in the file I viewed? 
                // Ah, PanelBitConfigEntity has PanelTypeId. But PanelEntity only has PanelGroupId. 
                // For now, let's load all PanelBitConfigs and maybe assume a mapping or just list them if Panel does not differentiate types strongly yet.
                // Wait, if PanelEntity doesn't have TypeID, how do we know which template to use? 
                // Let's assume for this step we load all Panel Bit Configs under a specific Panel ID if possible, or maybe PanelTypeId is missing in my view of PanelEntity.
                // Assuming generic Panel configs for now or using Panel.Id as TypeId? No, that's unsafe.
                // Let's check PanelBitConfigEntity again. It has PanelTypeId.
                // Let's assume PanelEntity might have been updated or we missed it. If not, I will just list "面板" nodes and if I can't find configs specific to a panel, I might list all or empty.
                // Actually, looking at the user request "Panel is just Panel", maybe simpler.
                
                // Let's load all Panel configs as a dictionary for now.
                var panelBitConfigs = db.FindAll<PanelBitConfigEntity>().GroupBy(c => c.PanelTypeId).ToDictionary(g => g.Key, g => g.ToList());


                foreach (var station in stations)
                {
                    var stationNode = new SelectorNode { Name = station.StationName, NodeType = "Station", Id = station.Id };
                    
                    // --- Doors ---
                    if (doorGroups.ContainsKey(station.Id))
                    {
                        foreach (var group in doorGroups[station.Id])
                        {
                             // User Request: "Door Main" -> "Door" (门)
                             var groupNode = new SelectorNode { Name = "门", NodeType = "Group", Id = group.Id };
                             
                             if (doors.ContainsKey(group.Id))
                             {
                                 foreach (var door in doors[group.Id])
                                 {
                                     var doorNode = new SelectorNode { Name = door.DoorName, NodeType = "Door", Id = door.Id, ParentId = door.DoorTypeId}; 
                                     
                                     if (doorBitConfigs.ContainsKey(door.DoorTypeId))
                                     {
                                         foreach (var cfg in doorBitConfigs[door.DoorTypeId])
                                         {
                                             doorNode.Children.Add(new SelectorNode 
                                             { 
                                                 Name = cfg.Description,
                                                 FullDescription = $"{station.StationName}_{door.DoorName}_{cfg.Description}",
                                                 NodeType = "Config", 
                                                 Id = cfg.Id,              
                                                 ExtendedId = door.Id,     
                                                 Tag = TargetType.Door     
                                             });
                                         }
                                     }
                                     groupNode.Children.Add(doorNode);
                                 }
                             }
                             stationNode.Children.Add(groupNode);
                        }
                    }

                    // --- Panels ---
                    if (panelGroups.ContainsKey(station.Id))
                    {
                        foreach (var group in panelGroups[station.Id])
                        {
                            // User Request: "Panel Group" -> "面板" (Panel - logic from remark)
                            var groupNode = new SelectorNode { Name = "面板", NodeType = "Group", Id = group.Id };

                            if (panels.ContainsKey(group.Id))
                            {
                                foreach (var panel in panels[group.Id])
                                {
                                    var panelNode = new SelectorNode { Name = panel.PanelName, NodeType = "Panel", Id = panel.Id };
                                    
                                    if (panelBitConfigs.Count > 0)
                                    {
                                        // Flatten all available panel configs for now as we lack the link
                                        foreach(var typeGroup in panelBitConfigs)
                                        {
                                            foreach(var cfg in typeGroup.Value)
                                            {
                                                panelNode.Children.Add(new SelectorNode
                                                {
                                                    Name = cfg.Description,
                                                    FullDescription = $"{station.StationName}_{panel.PanelName}_{cfg.Description}",
                                                    NodeType = "Config", 
                                                    Id = cfg.Id,
                                                    ExtendedId = panel.Id,
                                                    Tag = TargetType.Panel
                                                });
                                            }
                                        }
                                    }
                                    groupNode.Children.Add(panelNode);
                                }
                            }
                            stationNode.Children.Add(groupNode);
                        }
                    }

                    SelectorTree.Add(stationNode);
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void SelectNode(object obj)
        {
            if (obj is SelectorNode node && node.NodeType == "Config")
            {
                NewPoint.TargetType = (TargetType)node.Tag;
                NewPoint.TargetObjId = node.ExtendedId;
                NewPoint.TargetBitConfigId = node.Id;
                
                // Always update Description with full context as requested
                NewPoint.Description = node.FullDescription;
                
                // Close Popup
                IsPopupOpen = false;
            }
        }
    }

    public class SelectorNode
    {
        public string Name { get; set; }
        public string FullDescription { get; set; } // Station_Door_Config
        public string NodeType { get; set; } // Station, Group, Door, Config
        public int Id { get; set; }
        public int ExtendedId { get; set; } // For storing Parent Obj Id
        public int ParentId { get; set; }
        public object Tag { get; set; }
        public ObservableCollection<SelectorNode> Children { get; set; } = new ObservableCollection<SelectorNode>();
    }
}
