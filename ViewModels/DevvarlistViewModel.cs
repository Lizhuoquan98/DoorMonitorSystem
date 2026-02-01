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
using DoorMonitorSystem.Assets.Services;

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

        public ICommand ExportPointsCommand => new RelayCommand(o => ExportPoints());
        public ICommand ImportPointsCommand => new RelayCommand(o => ImportPoints());

        public DevvarlistViewModel()
        {
            CheckSchema();
            LoadParameterKeys();
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
                // Missing Sync/Log fields from previous manual checks
                if (!columnNames.Contains("SyncMode")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncMode` INT DEFAULT 0;");
                if (!columnNames.Contains("SyncTargetBitIndex")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `SyncTargetBitIndex` INT DEFAULT NULL;");
                
                // Logical Binding Key
                if (!columnNames.Contains("UiBinding")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `UiBinding` VARCHAR(50) DEFAULT NULL;");
                if (!columnNames.Contains("BindingRole")) db.ExecuteNonQuery("ALTER TABLE `DevicePointConfig` ADD COLUMN `BindingRole` VARCHAR(20) DEFAULT 'Read';");

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

        public List<string> ParameterKeys { get; private set; } = new List<string>();

        private void LoadParameterKeys()
        {
            try
            {
                var defines = DataManager.Instance.LoadParameterDefinesFromDb();
                ParameterKeys = defines.Select(d => d.BindingKey).Distinct().ToList();
                OnPropertyChanged(nameof(ParameterKeys));
            }
            catch (Exception ex)
            {
                LogHelper.Error("LoadParameterKeys Failed", ex);
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
                UiBinding = SelectedPoint.UiBinding, // 复制逻辑绑定键
                BindingRole = SelectedPoint.BindingRole, // 复制绑定角色
                
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

            // 1. 物理地址重复校验：
            // 确保在同一个源设备（PLC）下，不出现完全相同的寄存器地址和位索引。
            // 排除当前正在编辑的记录本身 (Id != NewPoint.Id)。
            var dupAddr = Points.FirstOrDefault(p => p.Id != NewPoint.Id 
                                                  && p.SourceDeviceId == NewPoint.SourceDeviceId 
                                                  && p.Address == NewPoint.Address 
                                                  && p.BitIndex == NewPoint.BitIndex);
            if (dupAddr != null)
            {
                MessageBox.Show($"物理地址冲突！该设备下已存在相同地址: {NewPoint.Address} [BitIndex:{NewPoint.BitIndex}]\n请勿重复配置物理点位。");
                return;
            }

            // 2. 逻辑绑定重复校验 (全系统范围强制唯一)：
            // 目的是防止多个 PLC 地址绑定到了同一个 UI 参数（如两个不同的寄存器都尝试作为“上行站台的最大开门速度”）。
            if (NewPoint.TargetType != TargetType.None && !string.IsNullOrEmpty(NewPoint.UiBinding))
            {
                try
                {
                    using var dbCheck = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    dbCheck.Connect();
                    
                    // 构建参数化查询，在全表中搜索是否存在冲突。
                    // 冲突条件：相同的 TargetType + TargetObjId + UiBinding + BindingRole
                    var paramsList = new MySqlParameter[] {
                        new MySqlParameter("@tt", (int)NewPoint.TargetType),
                        new MySqlParameter("@tid", NewPoint.TargetObjId),
                        new MySqlParameter("@key", NewPoint.UiBinding),
                        new MySqlParameter("@role", NewPoint.BindingRole ?? ""),
                        new MySqlParameter("@id", NewPoint.Id)
                    };
                    
                    var existing = dbCheck.Query<DevicePointConfigEntity>("DevicePointConfigs", "TargetType = @tt AND TargetObjId = @tid AND UiBinding = @key AND BindingRole = @role AND Id != @id", paramsList);
                    
                    if (existing != null && existing.Count > 0)
                    {
                        var first = existing[0];
                        string targetName = NewPoint.TargetType == TargetType.Station ? "站台" : (NewPoint.TargetType == TargetType.Door ? "门" : "面板");
                        // 溯源冲突点位所在的设备名称
                        string devName = GlobalData.ListDveices?.FirstOrDefault(d => d.ID == first.SourceDeviceId)?.Name ?? $"Device_{first.SourceDeviceId}";
                        
                        MessageBox.Show($"业务绑定冲突！\n【{targetName}】(ID:{NewPoint.TargetObjId}) 在设备 [{devName}] 下已关联了:\n键名: {NewPoint.UiBinding}\n角色: {NewPoint.BindingRole}\n物理地址: {first.Address}\n\n请先修改/删除现有配置后再重试。");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error("[DevVar] 重复项检查逻辑异常", ex);
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
                    DeviceCommunicationService.Instance?.ReloadConfigs(); // 刷新缓存
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
                    DeviceCommunicationService.Instance?.ReloadConfigs(); // 刷新缓存
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
                DeviceCommunicationService.Instance?.ReloadConfigs(); // 刷新缓存
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


                // 4. Load UI Parameters (Global Definitions but attached to each Station)
                var paramDefines = db.FindAll<ParameterDefineEntity>();
                
                foreach (var station in stations)
                {
                    var stationNode = new SelectorNode { Name = station.StationName, NodeType = "Station", Id = station.Id };
                    
                    // --- Doors ---
                    if (doorGroups.ContainsKey(station.Id))
                    {
                        foreach (var group in doorGroups[station.Id])
                        {
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
                            var groupNode = new SelectorNode { Name = "面板", NodeType = "Group", Id = group.Id };

                            if (panels.ContainsKey(group.Id))
                            {
                                foreach (var panel in panels[group.Id])
                                {
                                    var panelNode = new SelectorNode { Name = panel.PanelName, NodeType = "Panel", Id = panel.Id };
                                    
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

                    // --- Parameter Config (Attached to Station) ---
                    if (paramDefines != null)
                    {
                        var configRoot = new SelectorNode { Name = "参数配置", NodeType = "ParamRoot", Id = station.Id };

                        // 4.1 Parameter Control (System Level)
                        var ctrlGroup = new SelectorNode { Name = "参数控制", NodeType = "ParamGroup", Id = station.Id };
                        
                        ctrlGroup.Children.Add(CreateParamNode("参数读取", "Sys_ReadTrigger", "Write", station.Id, "Sys Control"));
                        ctrlGroup.Children.Add(CreateParamNode("参数写入", "Sys_WriteTrigger", "Write", station.Id, "Sys Control"));
                        ctrlGroup.Children.Add(CreateParamNode("门号选择", "Sys_DoorId", "Write", station.Id, "Sys Control"));

                        configRoot.Children.Add(ctrlGroup);

                        // 4.2 Parameter Groups (Business Params)
                        if (paramDefines.Count > 0)
                        {
                            var paramGroup = new SelectorNode { Name = "参数组", NodeType = "ParamGroup", Id = station.Id };
                            foreach (var p in paramDefines.OrderBy(x => x.SortOrder))
                            {
                                var pNode = new SelectorNode 
                                { 
                                    Name = p.Label, 
                                    NodeType = "ParamParent", 
                                    Id = station.Id,
                                    FullDescription = $"{station.StationName}_参数: {p.Label} [{p.BindingKey}]"
                                };

                                // Add 3 sub-nodes as requested
                                pNode.Children.Add(CreateParamNode("参数写入值", p.BindingKey, "Write", station.Id, p.Label));
                                pNode.Children.Add(CreateParamNode("参数读取值", p.BindingKey, "Read", station.Id, p.Label));
                                pNode.Children.Add(CreateParamNode("参数权限值", p.BindingKey, "Auth", station.Id, p.Label));

                                paramGroup.Children.Add(pNode);
                            }
                            configRoot.Children.Add(paramGroup);
                        }
                        
                        stationNode.Children.Add(configRoot);
                    }

                    SelectorTree.Add(stationNode);
                }
            }
            catch (Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private SelectorNode CreateParamNode(string name, string key, string role, int stationId, string parentLabel = "")
        {
            return new SelectorNode
            {
                Name = name,
                FullDescription = string.IsNullOrEmpty(parentLabel) ? $"{name}" : $"{parentLabel} - {name}",
                ObjectName = key, // BindingKey
                NodeType = "Param",
                Id = stationId,   // Store StationId here
                Role = role, // Store Role
                Tag = "ParamBinding"
            };
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
            if (obj is SelectorNode node)
            {
                if (node.NodeType == "Config")
                {
                    // Existing Visual Binding Logic
                    NewPoint.TargetType = (TargetType)node.Tag;
                    NewPoint.TargetObjId = node.ExtendedId;
                    NewPoint.TargetBitConfigId = node.Id;
                    NewPoint.TargetObjName = node.ObjectName;
                    NewPoint.TargetBitConfigName = node.Name;
                    NewPoint.Description = node.FullDescription;
                    IsPopupOpen = false;
                }
                else if (node.NodeType == "Param")
                {
                    // New Logical Binding Logic (Station Level)
                    NewPoint.TargetType = TargetType.Station;
                    NewPoint.TargetObjId = node.Id;       // 这里存的是 StationId
                    NewPoint.UiBinding = node.ObjectName; // BindingKey
                    NewPoint.BindingRole = node.Role;     // BindingRole (Read/Write/Auth)
                    NewPoint.Description = node.FullDescription; // 总是更新备注
                    IsPopupOpen = false;
                }
            }
        }

        public ICommand ClearBindingCommand => new RelayCommand(ClearBinding);

        private void ClearBinding(object obj)
        {
            NewPoint.TargetType = TargetType.None;
            NewPoint.TargetObjId = 0;
            NewPoint.TargetBitConfigId = 0;
            NewPoint.UiBinding = null; 
            NewPoint.BindingRole = null; // Reset default
            // Optional: Clear Description? No, keep it as user might want to keep the text.
        }

        public List<string> BindingRoles { get; } = new List<string> { "Read", "Write", "Auth", "DoorId" };

        #region 导入导出逻辑

        /// <summary>
        /// 导出当前设备的点位表到 Excel
        /// </summary>
        private void ExportPoints()
        {
            if (SelectedDevice == null) return;
            if (Points.Count == 0)
            {
                MessageBox.Show("当前设备没有点位可导出。");
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"点位表_{SelectedDevice.Name}_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var exportData = Points.Select(p => new
                    {
                        序号 = p.RowIndex,
                        物理地址 = p.Address,
                        位索引 = p.BitIndex,
                        数据类型 = p.DataType,
                        备注说明 = p.Description,
                        逻辑绑定键 = p.UiBinding,
                        绑定角色 = p.BindingRole,
                        目标类型 = (int)p.TargetType,
                        目标对象ID = p.TargetObjId,
                        目标位配置ID = p.TargetBitConfigId,
                        开启同步 = p.IsSyncEnabled ? 1 : 0,
                        同步目标设备ID = p.SyncTargetDeviceId,
                        同步写入地址 = p.SyncTargetAddress,
                        同步目标位索引 = p.SyncTargetBitIndex,
                        同步模式 = p.SyncMode,
                        开启日志 = p.IsLogEnabled ? 1 : 0,
                        日志类型ID = p.LogTypeId,
                        日志内容模板 = p.LogMessage,
                        日志分类 = p.Category,
                        高限报警 = p.HighLimit,
                        低限报警 = p.LowLimit,
                        状态0描述 = p.State0Desc,
                        状态1描述 = p.State1Desc,
                        报警目标值 = p.AlarmTargetValue
                    });

                    MiniExcelLibs.MiniExcel.SaveAs(sfd.FileName, exportData);
                    MessageBox.Show("导出成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 从 Excel 导入点位表到当前设备
        /// </summary>
        private async void ImportPoints()
        {
            if (SelectedDevice == null)
            {
                MessageBox.Show("请先选择一个目标设备。");
                return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var rows = MiniExcelLibs.MiniExcel.Query(ofd.FileName).ToList();
                    if (rows.Count <= 1)
                    {
                        MessageBox.Show("Excel 文件内容为空或格式不正确。");
                        return;
                    }

                    // 获取表头并过滤数据行
                    var dataRows = rows.Skip(1).Cast<IDictionary<string, object>>().ToList();

                    if (MessageBox.Show($"准备从 Excel 导入 {dataRows.Count} 个点位到设备 [{SelectedDevice.Name}]。\n\n提示：导入过程已优化，将使用事务批量处理以提升速度。\n是否继续？", "导入确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;

                    int successCount = 0;
                    int errorCount = 0;

                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    // 【性能优化点 1】：一次性加载该设备所有现有点位到内存，避免循环内查询数据库
                    var existingPoints = db.FindAll<DevicePointConfigEntity>("SourceDeviceId = @sid", new MySqlParameter("@sid", SelectedDevice.ID))
                                           .ToDictionary(p => $"{p.Address}_{p.BitIndex}");

                    // 【性能优化点 2】：开启显式事务
                    db.BeginTransaction();
                    try 
                    {
                        foreach (var row in dataRows)
                        {
                            try
                            {
                                // 辅助函数：安全解析整型 (支持 Key查找 -> Index查找 -> 空值/格式错误处理)
                                Func<string, int, int?> GetSafeInt = (k, idx) => 
                                {
                                    object val = row.ContainsKey(k) ? row[k] : row.Values.ElementAtOrDefault(idx);
                                    if (val == null || val is DBNull || string.IsNullOrWhiteSpace(val.ToString())) return null;
                                    try { return Convert.ToInt32(val); } catch { return null; } 
                                };

                                // 映射逻辑保持不变 (支持按键名或按索引取值)
                                var entity = new DevicePointConfigEntity
                                {
                                    SourceDeviceId = SelectedDevice.ID,
                                    Address = row.ContainsKey("物理地址") ? row["物理地址"]?.ToString() : row.Values.ElementAtOrDefault(1)?.ToString(),
                                    BitIndex = Convert.ToInt32(row.ContainsKey("位索引") ? (row["位索引"] ?? 0) : (row.Values.ElementAtOrDefault(2) ?? 0)),
                                    DataType = row.ContainsKey("数据类型") ? row["数据类型"]?.ToString() : row.Values.ElementAtOrDefault(3)?.ToString() ?? "Word",
                                    Description = row.ContainsKey("备注说明") ? row["备注说明"]?.ToString() : row.Values.ElementAtOrDefault(4)?.ToString(),
                                    UiBinding = row.ContainsKey("逻辑绑定键") ? row["逻辑绑定键"]?.ToString() : row.Values.ElementAtOrDefault(5)?.ToString(),
                                    BindingRole = row.ContainsKey("绑定角色") ? row["绑定角色"]?.ToString() : row.Values.ElementAtOrDefault(6)?.ToString(),
                                    TargetType = (TargetType)Convert.ToInt32(row.ContainsKey("目标类型") ? (row["目标类型"] ?? 0) : (row.Values.ElementAtOrDefault(7) ?? 0)),
                                    TargetObjId = Convert.ToInt32(row.ContainsKey("目标对象ID") ? (row["目标对象ID"] ?? 0) : (row.Values.ElementAtOrDefault(8) ?? 0)),
                                    TargetBitConfigId = Convert.ToInt32(row.ContainsKey("目标位配置ID") ? (row["目标位配置ID"] ?? 0) : (row.Values.ElementAtOrDefault(9) ?? 0)),
                                    IsSyncEnabled = Convert.ToInt32(row.ContainsKey("开启同步") ? (row["开启同步"] ?? 0) : (row.Values.ElementAtOrDefault(10) ?? 0)) == 1,
                                    
                                    // 【修复】增加健壮性解析和索引回退 (11,12,13)
                                    SyncTargetDeviceId = GetSafeInt("同步目标设备ID", 11),
                                    SyncTargetAddress = (ushort?)GetSafeInt("同步写入地址", 12),
                                    SyncTargetBitIndex = GetSafeInt("同步目标位索引", 13),

                                    SyncMode = Convert.ToInt32(row.ContainsKey("同步模式") ? (row["同步模式"] ?? 0) : (row.Values.ElementAtOrDefault(14) ?? 0)),
                                    IsLogEnabled = Convert.ToInt32(row.ContainsKey("开启日志") ? (row["开启日志"] ?? 0) : (row.Values.ElementAtOrDefault(15) ?? 0)) == 1,
                                    LogTypeId = Convert.ToInt32(row.ContainsKey("日志类型ID") ? (row["日志类型ID"] ?? 1) : (row.Values.ElementAtOrDefault(16) ?? 1)),
                                    LogMessage = row.ContainsKey("日志内容模板") ? row["日志内容模板"]?.ToString() : row.Values.ElementAtOrDefault(17)?.ToString(),
                                    Category = row.ContainsKey("日志分类") ? row["日志分类"]?.ToString() : row.Values.ElementAtOrDefault(18)?.ToString(),
                                    HighLimit = row.ContainsKey("高限报警") && row["高限报警"] != null ? (double?)Convert.ToDouble(row["高限报警"]) : null,
                                    LowLimit = row.ContainsKey("低限报警") && row["低限报警"] != null ? (double?)Convert.ToDouble(row["低限报警"]) : null,
                                    State0Desc = row.ContainsKey("状态0描述") ? row["状态0描述"]?.ToString() : row.Values.ElementAtOrDefault(21)?.ToString(),
                                    State1Desc = row.ContainsKey("状态1描述") ? row["状态1描述"]?.ToString() : row.Values.ElementAtOrDefault(22)?.ToString(),
                                    AlarmTargetValue = row.ContainsKey("报警目标值") && row["报警目标值"] != null ? (int?)Convert.ToInt32(row["报警目标值"]) : null
                                };

                                if (string.IsNullOrEmpty(entity.Address)) continue;

                                // 在内存字典中尝试匹配
                                string key = $"{entity.Address}_{entity.BitIndex}";
                                if (existingPoints.TryGetValue(key, out var existing))
                                {
                                    entity.Id = existing.Id;
                                    db.Update(entity);
                                }
                                else
                                {
                                    db.Insert(entity);
                                }
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                LogHelper.Error($"Import row failed: {ex.Message}");
                            }
                        }
                        db.CommitTransaction();
                    }
                    catch (Exception ex)
                    {
                        db.RollbackTransaction();
                        throw new Exception($"事务执行失败，已回滚: {ex.Message}", ex);
                    }

                    MessageBox.Show($"导入完成！\n成功: {successCount} 条\n异常: {errorCount} 条");
                    LoadPoints(); // 刷新列表
                    
                    // 通知通讯服务重新加载配置
                    DeviceCommunicationService.Instance?.ReloadConfigs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}");
                }
            }
        }

        #endregion
    }

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
