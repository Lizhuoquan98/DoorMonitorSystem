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

        // Editing State
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set 
            { 
                _isEditing = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ActionName)); // "添加" vs "保存"
            }
        }

        public string ActionName => IsEditing ? "保存" : "添加";

        // Commands
        public ICommand LoadDevicesCommand => new RelayCommand(o => LoadDevices());
        public ICommand AddPointCommand => new RelayCommand(AddPoint);
        public ICommand DeletePointCommand => new RelayCommand(DeletePoint);

        
        public ICommand StartEditCommand => new RelayCommand(StartEdit);
        public ICommand CancelEditCommand => new RelayCommand(CancelEdit);

        public DevvarlistViewModel()
        {
            CheckSchema();
            LoadDevices();
        }

        private void CheckSchema()
        {
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return; // Database might not exist yet
                db.Connect();

                if (!db.TableExists("DevicePointConfig")) return; // Table not created yet

                var columns = db.GetTableColumns("DevicePointConfig");
                var columnNames = new HashSet<string>();
                foreach (System.Data.DataRow row in columns.Rows)
                {
                    columnNames.Add(row["列名"].ToString());
                }

                // Auto-add missing columns
                // LogDeadband
                if (!columnNames.Contains("LogDeadband")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `LogDeadband` DOUBLE DEFAULT NULL;");

                // Category
                if (!columnNames.Contains("Category")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `Category` VARCHAR(50) DEFAULT NULL;");

                // Analog Alarm
                if (!columnNames.Contains("HighLimit")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `HighLimit` DOUBLE DEFAULT NULL;");
                if (!columnNames.Contains("LowLimit")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `LowLimit` DOUBLE DEFAULT NULL;");
                
                // Boolean State
                if (!columnNames.Contains("State0Desc")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `State0Desc` VARCHAR(50) DEFAULT NULL;");
                if (!columnNames.Contains("State1Desc")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `State1Desc` VARCHAR(50) DEFAULT NULL;");
                if (!columnNames.Contains("AlarmTargetValue")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `AlarmTargetValue` INT DEFAULT NULL;");
                
                // Missing Sync/Log fields from previous manual checks
                if (!columnNames.Contains("SyncMode")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncMode` INT DEFAULT 0;");
                if (!columnNames.Contains("SyncTargetBitIndex")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetBitIndex` INT DEFAULT NULL;");
                if (!columnNames.Contains("SyncMode")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncMode` INT DEFAULT 0;");
                if (!columnNames.Contains("SyncTargetBitIndex")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetBitIndex` INT DEFAULT NULL;");

                // Panel Table Upgrade
                if (db.TableExists("Panel"))
                {
                    var pCols = db.GetTableColumns("Panel");
                    var pColNames = new HashSet<string>();
                    foreach (System.Data.DataRow row in pCols.Rows) pColNames.Add(row["列名"].ToString());
                    if (!pColNames.Contains("PanelTypeId")) db.ExecuteNonQuery("ALTER TABLE `Panel` ADD COLUMN `PanelTypeId` INT DEFAULT 0;");
                }
            }
            catch (Exception ex)
            {
                // Silent catch or log? Better to log or ignore during development if DB isn't ready
                System.Diagnostics.Debug.WriteLine($"Schema Check Failed: {ex.Message}");
            }
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

                // Pre-load lookups
                var doors = db.FindAll<DoorEntity>().ToDictionary(d => d.Id);
                var panels = db.FindAll<PanelEntity>().ToDictionary(p => p.Id);
                var doorConfigs = db.FindAll<DoorBitConfigEntity>().ToDictionary(c => c.Id);
                var panelConfigs = db.FindAll<PanelBitConfigEntity>().ToDictionary(c => c.Id);

                int index = 1;
                foreach (var p in list)
                {
                    // 1. Virtual Row Index
                    p.RowIndex = index++;

                    // 2. Resolve Reader-Friendly Names
                    if (p.TargetType == TargetType.Door && doors.ContainsKey(p.TargetObjId))
                    {
                         p.TargetObjName = doors[p.TargetObjId].DoorName;
                         if (doorConfigs.ContainsKey(p.TargetBitConfigId))
                             p.TargetBitConfigName = doorConfigs[p.TargetBitConfigId].Description;
                    }
                    else if (p.TargetType == TargetType.Panel && panels.ContainsKey(p.TargetObjId))
                    {
                         p.TargetObjName = panels[p.TargetObjId].PanelName;
                         if (panelConfigs.ContainsKey(p.TargetBitConfigId))
                             p.TargetBitConfigName = panelConfigs[p.TargetBitConfigId].Description;
                    }

                    if (p.IsSyncEnabled && p.SyncTargetDeviceId.HasValue)
                    {
                        var dev = Devices.FirstOrDefault(d => d.ID == p.SyncTargetDeviceId.Value);
                        if (dev != null) p.SyncTargetDeviceName = dev.Name;
                    }

                    Points.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载点表失败: {ex.Message}");
            }
        }

        private void StartEdit(object obj)
        {
            if (SelectedPoint == null)
            {
                MessageBox.Show("请先选择要修改的点位");
                return;
            }

            // Copy SelectedPoint to NewPoint for editing
            NewPoint = new DevicePointConfigEntity
            {
                Id = SelectedPoint.Id,
                SourceDeviceId = SelectedPoint.SourceDeviceId,
                Address = SelectedPoint.Address,
                DataType = SelectedPoint.DataType,
                FunctionCode = SelectedPoint.FunctionCode,
                BitIndex = SelectedPoint.BitIndex,
                
                TargetType = SelectedPoint.TargetType,
                TargetObjId = SelectedPoint.TargetObjId,
                TargetBitConfigId = SelectedPoint.TargetBitConfigId,
                TargetObjName = SelectedPoint.TargetObjName,
                TargetBitConfigName = SelectedPoint.TargetBitConfigName,
                
                IsSyncEnabled = SelectedPoint.IsSyncEnabled,
                SyncTargetDeviceId = SelectedPoint.SyncTargetDeviceId,
                SyncTargetAddress = SelectedPoint.SyncTargetAddress,
                SyncTargetBitIndex = SelectedPoint.SyncTargetBitIndex,
                SyncMode = SelectedPoint.SyncMode,
                SyncTargetDeviceName = SelectedPoint.SyncTargetDeviceName,
                
                IsLogEnabled = SelectedPoint.IsLogEnabled,
                LogTypeId = SelectedPoint.LogTypeId,
                LogTriggerState = SelectedPoint.LogTriggerState,
                LogMessage = SelectedPoint.LogMessage,
                LogDeadband = SelectedPoint.LogDeadband,
                Category = SelectedPoint.Category,
                
                HighLimit = SelectedPoint.HighLimit,
                LowLimit = SelectedPoint.LowLimit,
                
                State0Desc = SelectedPoint.State0Desc,
                State1Desc = SelectedPoint.State1Desc,
                AlarmTargetValue = SelectedPoint.AlarmTargetValue,
                
                Description = SelectedPoint.Description
            };

            IsEditing = true;
        }

        private void CancelEdit(object obj)
        {
             ResetForm();
        }

        private void ResetForm()
        {
            if (SelectedDevice == null) return;
            
            IsEditing = false;
            NewPoint = new DevicePointConfigEntity 
            { 
                SourceDeviceId = SelectedDevice.ID,
                DataType = "Word",
                TargetType = TargetType.None,
                IsSyncEnabled = false
            };
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
            
            // Set Sync Device Name for display
            if (NewPoint.IsSyncEnabled && NewPoint.SyncTargetDeviceId.HasValue)
            {
                 var dev = Devices.FirstOrDefault(d => d.ID == NewPoint.SyncTargetDeviceId.Value);
                 if (dev != null) NewPoint.SyncTargetDeviceName = dev.Name;
            }

            // check duplication if inserting (not editing)
            if (!IsEditing)
            {
                var existing = Points.FirstOrDefault(p => p.SourceDeviceId == NewPoint.SourceDeviceId && 
                                                       p.Address == NewPoint.Address && 
                                                       p.BitIndex == NewPoint.BitIndex);
                if (existing != null)
                {
                     MessageBox.Show($"该设备下地址 '{NewPoint.Address}' (Bit {NewPoint.BitIndex}) 已存在！");
                     return;
                }
            }

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                if (IsEditing)
                {
                    // Update
                    db.Update(NewPoint);
                    
                    // Refresh List Item
                    var itemToUpdate = Points.FirstOrDefault(p => p.Id == NewPoint.Id);
                    if (itemToUpdate != null)
                    {
                        var index = Points.IndexOf(itemToUpdate);
                        // Update properties manually or replace
                        NewPoint.RowIndex = itemToUpdate.RowIndex; // Keep Index
                        Points[index] = NewPoint; 
                    }
                    MessageBox.Show("修改成功");
                    ResetForm();
                }
                else
                {
                    // Insert
                    db.Insert(NewPoint);
                    // Refresh List
                    NewPoint.RowIndex = Points.Count + 1;
                    Points.Add(NewPoint);
                    // Reset, keep some fields
                    NewPoint = new DevicePointConfigEntity 
                    { 
                        SourceDeviceId = SelectedDevice.ID,
                        DataType = NewPoint.DataType,
                        TargetType = NewPoint.TargetType,
                        IsSyncEnabled = false
                    };
                }
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"{(IsEditing ? "修改" : "添加")}失败: {ex.Message}");
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
                                                 ObjectName = door.DoorName, // Set Object Name
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
                                    
                                    // 修正逻辑: 数据库现有设计中，PanelBitConfig.PanelTypeId 实际上存储的是 Panel.Id
                                    // 参见 StationDataService.cs Line 389: new { PanelTypeId = panelId }
                                    if (panelBitConfigs.ContainsKey(panel.Id))
                                    {
                                        foreach(var cfg in panelBitConfigs[panel.Id])
                                        {
                                            AddPanelConfigNode(panelNode, station, panel, cfg);
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

        private void AddPanelConfigNode(SelectorNode parentNode, StationEntity station, PanelEntity panel, PanelBitConfigEntity cfg)
        {
            parentNode.Children.Add(new SelectorNode
            {
                Name = cfg.Description,
                FullDescription = $"{station.StationName}_{panel.PanelName}_{cfg.Description}",
                ObjectName = panel.PanelName,
                NodeType = "Config",
                Id = cfg.Id,
                ExtendedId = panel.Id,
                Tag = TargetType.Panel
            });
        }

        private void SelectNode(object obj)
        {
            if (obj is SelectorNode node && node.NodeType == "Config")
            {
                NewPoint.TargetType = (TargetType)node.Tag;
                NewPoint.TargetObjId = node.ExtendedId;
                NewPoint.TargetBitConfigId = node.Id;
                
                // Display Names
                NewPoint.TargetObjName = node.ObjectName;
                NewPoint.TargetBitConfigName = node.Name;

                // Always update Description with full context as requested
                NewPoint.Description = node.FullDescription;
                
                // Close Popup
                IsPopupOpen = false;
            }
        }

        public ICommand ClearBindingCommand => new RelayCommand(ClearBinding);

        private void ClearBinding(object obj)
        {
            NewPoint.TargetType = TargetType.None;
            NewPoint.TargetObjId = 0;
            NewPoint.TargetBitConfigId = 0;
            // Optional: Clear Description? No, keep it as user might want to keep the text.
        }
    }

    public class SelectorNode
    {
        public string Name { get; set; }
        public string FullDescription { get; set; } // Station_Door_Config
        public string ObjectName { get; set; } // New Property for Name Lookup
        public string NodeType { get; set; } // Station, Group, Door, Config
        public int Id { get; set; }
        public int ExtendedId { get; set; } // For storing Parent Obj Id
        public int ParentId { get; set; }
        public object Tag { get; set; }
        public ObservableCollection<SelectorNode> Children { get; set; } = new ObservableCollection<SelectorNode>();
    }
}
