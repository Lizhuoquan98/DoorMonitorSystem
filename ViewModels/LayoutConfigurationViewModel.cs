using Base;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Helper;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System;
using System.Linq;
using System.Collections.Generic;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models.system;
using DoorMonitorSystem.Models.ConfigEntity.Log;

namespace DoorMonitorSystem.ViewModels
{
    using DoorMonitorSystem.ViewModels.ConfigItems;

    // --- 主 ViewModel ---

    public class LayoutConfigurationViewModel : NotifyPropertyChanged
    {
        #region 属性

        public ObservableCollection<StationConfigItem> Stations { get; set; } = new();

        private ConfigItemBase _selectedItem;
        public ConfigItemBase SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
                
                // 使用 CommandManager 触发重新计算
                CommandManager.InvalidateRequerySuggested();
            }
        }
        
        // 动态加载的类型列表
        public ObservableCollection<StationTypeEntity> StationTypes { get; set; } = new();
        public ObservableCollection<DoorTypeEntity> DoorTypes { get; set; } = new();
        public ObservableCollection<PanelTypeEntity> PanelTypes { get; set; } = new();

        // 基础数据字典 (用于下拉框选择)
        public ObservableCollection<BitCategoryEntity> BitCategories { get; set; } = new();
        public ObservableCollection<BitColorEntity> BitColors { get; set; } = new();
        public ObservableCollection<SysGraphicGroupEntity> GraphicGroups { get; set; } = new();
        public ObservableCollection<LogTypeEntity> LogTypes { get; set; } = new();

        public Dictionary<string, ObservableCollection<DoorBitConfigEntity>> DoorBitConfigsByTypeMap { get; set; } = new();
        public Dictionary<string, ObservableCollection<PanelBitConfigEntity>> PanelBitConfigsByPanelMap { get; set; } = new();

        private string _globalSelectedDoorTypeKeyId;
        public string GlobalSelectedDoorTypeKeyId
        {
            get => _globalSelectedDoorTypeKeyId;
            set
            {
                if (_globalSelectedDoorTypeKeyId != value)
                {
                    _globalSelectedDoorTypeKeyId = value;
                    OnPropertyChanged();

                    if (SelectedItem is DoorGroupConfigItem item)
                    {
                        item.RaisePropertyChangeForDoorType();
                    }
                }
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        #endregion

        #region 命令

        public ICommand LoadCommand { get; }
        public ICommand LoadDataCommand { get; }
        public ICommand SaveCommand { get; }
        
        public ICommand AddStationCommand { get; }
        public ICommand AddGroupCommand { get; }     
        public ICommand AddDoorGroupCommand { get; }
        public ICommand AddPanelGroupCommand { get; }
        
        public ICommand AddDoorCommand { get; }
        public ICommand AddPanelCommand { get; }
        
        public ICommand RemoveCommand { get; }
        public ICommand SelectedItemChangedCommand { get; }

        public ICommand ShowCategoryManagerCommand { get; }
        public ICommand ShowColorManagerCommand { get; }

        #endregion

        public LayoutConfigurationViewModel()
        {
            LoadCommand = new RelayCommand(LoadData);
            SaveCommand = new RelayCommand(SaveData);
            
            AddStationCommand = new RelayCommand(_ => AddStation());
            
            AddDoorGroupCommand = new RelayCommand(_ => AddDoorGroup(), _ => SelectedItem is StationConfigItem);
            AddPanelGroupCommand = new RelayCommand(_ => AddPanelGroup(), _ => SelectedItem is StationConfigItem);
            
            AddGroupCommand = new RelayCommand(_ => AddDoorGroup(), _ => SelectedItem is StationConfigItem); 

            AddDoorCommand = new RelayCommand(_ => AddDoor(), _ => SelectedItem is DoorGroupConfigItem);
            AddPanelCommand = new RelayCommand(_ => AddPanel(), _ => SelectedItem is PanelGroupConfigItem);
            
            RemoveCommand = new RelayCommand(_ => RemoveItem(), _ => SelectedItem != null);
            
            SelectedItemChangedCommand = new RelayCommand(param => SelectedItem = param as ConfigItemBase);
            LoadDataCommand = new RelayCommand(LoadData);

            ShowCategoryManagerCommand = new RelayCommand(_ => ShowDictionaryManager("Category"));
            ShowColorManagerCommand = new RelayCommand(_ => ShowDictionaryManager("Color"));

            LoadData(null);
        }

        #region 方法

        private void LoadData(object obj)
        {
            StatusMessage = "正在加载配置...";
            
            if (GlobalData.SysCfg == null) return;

            // 确保业务数据库结构最新 (包含 KeyId 迁移和新增字段)
            BusinessDatabaseFixer.FixSchema(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);

            Stations.Clear();
            StationTypes.Clear();
            DoorTypes.Clear();
            PanelTypes.Clear();
            BitCategories.Clear();
            BitColors.Clear();
            LogTypes.Clear(); // 必须清除，否则每次刷新都会重复
            GraphicGroups.Clear();
            PanelBitConfigsByPanelMap.Clear();
            DoorBitConfigsByTypeMap.Clear();
            
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                // 检查并升级数据库结构
                CheckAndUpgradeDatabase(db);
                db.CreateTableFromModel<LogTypeEntity>();

                // --- 1. 加载 StationType ---
                try 
                {
                    var sTypes = db.Query<StationTypeEntity>("SELECT * FROM StationType");
                    if (sTypes.Count == 0)
                    {
                        db.ExecuteNonQuery("INSERT INTO StationType (Name, Code) VALUES ('岛式站台', 'Island'), ('侧式站台', 'Side')");
                        sTypes = db.Query<StationTypeEntity>("SELECT * FROM StationType");
                    }
                    foreach (var t in sTypes) StationTypes.Add(t);
                }
                catch
                {
                    // 忽略错误，或创建表（在 CheckAndUpgradeDatabase 中处理）
                }

                // --- 2. 加载 DoorType ---
                try
                {
                    var dTypes = db.Query<DoorTypeEntity>("SELECT * FROM DoorType");
                    if (dTypes.Count == 0)
                    {
                        // 包含 KeyId 的初始化
                        var k1 = Guid.NewGuid().ToString();
                        var k2 = Guid.NewGuid().ToString();
                        var k3 = Guid.NewGuid().ToString();
                        db.ExecuteNonQuery($"INSERT INTO DoorType (Name, Code, KeyId) VALUES ('滑动门', 'Sliding', '{k1}'), ('应急门', 'Emergency', '{k2}'), ('端门', 'End', '{k3}')");
                        dTypes = db.Query<DoorTypeEntity>("SELECT * FROM DoorType");
                    }
                    foreach (var t in dTypes)
                    {
                         if(string.IsNullOrEmpty(t.KeyId)) t.KeyId = Guid.NewGuid().ToString(); // 补全 KeyId
                         DoorTypes.Add(t);
                    }
                    
                    // 默认选中第一个门类型
                    if (DoorTypes.Count > 0)
                    {
                        GlobalSelectedDoorTypeKeyId = DoorTypes[0].KeyId;
                    }
                }
                catch { }

                // --- 2.5 加载 PanelType ---
                try
                {
                    var pTypes = db.Query<PanelTypeEntity>("SELECT * FROM PanelType");
                    if (pTypes.Count == 0)
                    {
                        var k1 = Guid.NewGuid().ToString();
                        db.ExecuteNonQuery($"INSERT INTO PanelType (Name, Code, KeyId) VALUES ('状态面板', 'Status', '{k1}')");
                        pTypes = db.Query<PanelTypeEntity>("SELECT * FROM PanelType");
                    }
                    foreach (var t in pTypes)
                    {
                        if (string.IsNullOrEmpty(t.KeyId)) t.KeyId = Guid.NewGuid().ToString();
                        PanelTypes.Add(t);
                    }
                }
                catch { }

                // --- 2.6 加载字典数据 (用于下拉框) ---
                try
                {
                    // 检查是否已经执行过基础字典数据的初始化导入 (通过系统配置表记录状态)
                    var isImported = false;
                    try
                    {
                        var initValue = db.ExecuteScalar("SELECT SettingValue FROM SysSettingsEntity WHERE SettingKey = 'App.InitialDataImported'")?.ToString();
                        isImported = (initValue == "True");
                    }
                    catch { /* 如果表不存在或其它原因，保守起见视为未导入 */ }

                    if (!isImported)
                    {
                        LogHelper.Info("[LayoutConfig] 检测到首次运行或安装，正在预制基础字典数据...");

                        // 1. 预制分类数据 (如果已存在则跳过，防止打包时已有部分数据的情况)
                        var existCategories = db.Query<BitCategoryEntity>("SELECT * FROM BitCategory").Select(x => x.Name).ToList();
                        var defaultCategories = new[] 
                        { 
                            ("故障", "Fault", "⚠️", "#FF4444", "#FFFFFF", 1, 0, 2), 
                            ("报警", "Alarm", "⚡", "#FFBB33", "#000000", 2, 0, 2), 
                            ("状态", "Status", "ℹ️", "#33B5E5", "#FFFFFF", 3, 0, 2) 
                        };
                        foreach (var cat in defaultCategories)
                        {
                            if (!existCategories.Contains(cat.Item1))
                            {
                                string sql = $"INSERT INTO BitCategory (Name, Code, Icon, BackgroundColor, ForegroundColor, SortOrder, LayoutRows, LayoutColumns) " +
                                             $"VALUES ('{cat.Item1}', '{cat.Item2}', '{cat.Item3}', '{cat.Item4}', '{cat.Item5}', {cat.Item6}, {cat.Item7}, {cat.Item8})";
                                db.ExecuteNonQuery(sql);
                            }
                        }

                        // 2. 预制颜色数据
                        var existColors = db.Query<BitColorEntity>("SELECT * FROM BitColor").Select(x => x.ColorName).ToList();
                        var colorValues = new[]
                        {
                            ("透明", "#00000000"), ("绿色", "#FF00FF00"), ("红色", "#FFFF0000"), 
                            ("黄色", "#FFFFFF00"), ("蓝色", "#FF0000FF"), ("白色", "#FFFFFFFF"), 
                            ("灰色", "#FF808080"), ("黑色", "#FF000000"), ("橙色", "#FFFFA500"), 
                            ("青色", "#FF00FFFF")
                        };
                        foreach (var cv in colorValues)
                        {
                            if (!existColors.Contains(cv.Item1))
                            {
                                db.ExecuteNonQuery($"INSERT INTO BitColor (ColorName, ColorValue) VALUES ('{cv.Item1}', '{cv.Item2}')");
                            }
                        }
                        
                        // 3. 预制日志类型
                        try 
                        {
                            var logTypeCount = db.ExecuteScalar("SELECT COUNT(1) FROM LogType");
                            if (Convert.ToInt32(logTypeCount) == 0)
                            {
                                db.ExecuteNonQuery("INSERT INTO LogType (Name, SortOrder) VALUES ('状态', 1)");
                                db.ExecuteNonQuery("INSERT INTO LogType (Name, SortOrder) VALUES ('报警', 2)");
                                LogHelper.Info("[LayoutConfig] 预制了基础日志类型。");
                            }
                        }
                        catch (Exception ex) { LogHelper.Error("[LayoutConfig] 预制日志类型失败", ex); }

                        // 3. 写入初始化标记，确保下次不再执行
                        db.ExecuteNonQuery("INSERT INTO SysSettingsEntity (Category, SettingKey, SettingValue, Description) VALUES ('System', 'App.InitialDataImported', 'True', '基础字典数据初始化标记') ON DUPLICATE KEY UPDATE SettingValue='True'");
                        LogHelper.Info("[LayoutConfig] 基础字典数据初始化完成。");
                    }

                    // 无论是否初始化，最后加载一遍数据供 UI 使用
                    var categories = db.Query<BitCategoryEntity>("SELECT * FROM BitCategory ORDER BY SortOrder");
                    foreach (var c in categories) BitCategories.Add(c);

                    var colors = db.Query<BitColorEntity>("SELECT * FROM BitColor ORDER BY Id");
                    foreach (var c in colors) BitColors.Add(c);

                    var graphics = db.Query<SysGraphicGroupEntity>("SELECT * FROM SysGraphicGroupEntity ORDER BY GroupName");
                    foreach (var g in graphics) GraphicGroups.Add(g);

                    var logTypes = db.Query<LogTypeEntity>("SELECT * FROM LogType ORDER BY Id");
                    foreach (var l in logTypes) LogTypes.Add(l);
                }
                catch (Exception ex)
                {
                    LogHelper.Error("[LayoutConfig] 加载/初始化字典数据失败", ex);
                }

                // --- 3. 加载实体数据 ---
                var stationEntities = db.Query<StationEntity>("SELECT * FROM Station ORDER BY Id");
                var doorGroupEntities = db.Query<DoorGroupEntity>("SELECT * FROM DoorGroup ORDER BY SortOrder");
                var doorEntities = db.Query<DoorEntity>("SELECT * FROM Door ORDER BY SortOrder");
                var panelGroupEntities = db.Query<PanelGroupEntity>("SELECT * FROM PanelGroup ORDER BY SortOrder");
                var panelEntities = db.Query<PanelEntity>("SELECT * FROM Panel ORDER BY SortOrder");

                // --- 4. 补全 Entity KeyId (防空) ---
                foreach (var e in stationEntities) if (string.IsNullOrEmpty(e.KeyId)) e.KeyId = Guid.NewGuid().ToString();
                foreach (var e in doorGroupEntities) if (string.IsNullOrEmpty(e.KeyId)) e.KeyId = Guid.NewGuid().ToString();
                foreach (var e in doorEntities) if (string.IsNullOrEmpty(e.KeyId)) e.KeyId = Guid.NewGuid().ToString();
                foreach (var e in panelGroupEntities) if (string.IsNullOrEmpty(e.KeyId)) e.KeyId = Guid.NewGuid().ToString();
                foreach (var e in panelEntities) if (string.IsNullOrEmpty(e.KeyId)) e.KeyId = Guid.NewGuid().ToString();

                // --- 5. 构建 ConfigItem 树 (KeyId 关联) ---
                
                // 5.1 准备站台容器
                var stationConfigItems = stationEntities.Select(s => new StationConfigItem(s)).ToList();
                var stationKeyMap = stationConfigItems.ToDictionary(i => i.Entity.KeyId);

                // 5.2 加载门点位映射 (用于全局共享点表)
                var doorBitEntities = db.Query<DoorBitConfigEntity>("SELECT * FROM DoorBitConfig ORDER BY SortOrder");
                DoorBitConfigsByTypeMap = doorBitEntities.GroupBy(b => b.DoorTypeKeyId)
                                                  .ToDictionary(g => g.Key, g => new ObservableCollection<DoorBitConfigEntity>(g));

                // 5.3 门组 -> 站台
                var doorGroupItems = new List<DoorGroupConfigItem>();
                foreach (var g in doorGroupEntities)
                {
                    if (!string.IsNullOrEmpty(g.StationKeyId) && stationKeyMap.ContainsKey(g.StationKeyId))
                    {
                        var parent = stationKeyMap[g.StationKeyId];
                        var item = new DoorGroupConfigItem(g, parent, DoorBitConfigsByTypeMap, this);
                        parent.Children.Add(item);
                        doorGroupItems.Add(item);
                    }
                }
                var doorGroupKeyMap = doorGroupItems.ToDictionary(i => i.Entity.KeyId);

                // 5.3 面板组 -> 站台
                var panelGroupItems = new List<PanelGroupConfigItem>();
                foreach (var g in panelGroupEntities)
                {
                    if (!string.IsNullOrEmpty(g.StationKeyId) && stationKeyMap.ContainsKey(g.StationKeyId))
                    {
                        var parent = stationKeyMap[g.StationKeyId];
                        var item = new PanelGroupConfigItem(g, parent);
                        parent.Children.Add(item);
                        panelGroupItems.Add(item);
                    }
                }
                var panelGroupKeyMap = panelGroupItems.ToDictionary(i => i.Entity.KeyId);

                // 5.4 门 -> 门组
                var doorItems = new List<DoorConfigItem>();
                foreach (var d in doorEntities)
                {
                    if (!string.IsNullOrEmpty(d.ParentKeyId) && doorGroupKeyMap.ContainsKey(d.ParentKeyId))
                    {
                         var parent = doorGroupKeyMap[d.ParentKeyId];
                         var doorItem = new DoorConfigItem(d, parent);
                         parent.Children.Add(doorItem);
                         doorItems.Add(doorItem);
                    }
                }

                // 5.5 加载面板点位映射
                var panelBitEntities = db.Query<PanelBitConfigEntity>("SELECT * FROM PanelBitConfig ORDER BY SortOrder");
                PanelBitConfigsByPanelMap = panelBitEntities.GroupBy(b => b.PanelKeyId)
                                                  .ToDictionary(g => g.Key, g => new ObservableCollection<PanelBitConfigEntity>(g));

                // 5.6 面板 -> 面板组
                foreach (var p in panelEntities)
                {
                    if (!string.IsNullOrEmpty(p.ParentKeyId) && panelGroupKeyMap.ContainsKey(p.ParentKeyId))
                    {
                        var parent = panelGroupKeyMap[p.ParentKeyId];
                        parent.Children.Add(new PanelConfigItem(p, parent, PanelBitConfigsByPanelMap, this));
                    }
                }

                foreach (var door in doorItems)
                {
                    if (!string.IsNullOrEmpty(door.Entity.DoorTypeKeyId))
                    {
                        if (DoorBitConfigsByTypeMap.ContainsKey(door.Entity.DoorTypeKeyId))
                        {
                            door.BitConfigs = DoorBitConfigsByTypeMap[door.Entity.DoorTypeKeyId];
                        }
                        else
                        {
                            var newBits = new ObservableCollection<DoorBitConfigEntity>();
                            DoorBitConfigsByTypeMap[door.Entity.DoorTypeKeyId] = newBits;
                            door.BitConfigs = newBits;
                        }
                    }
                }

                // --- 6. 刷新界面 ---
                foreach (var item in stationConfigItems) Stations.Add(item);

                StatusMessage = $"配置加载完成，共 {Stations.Count} 个站台";

                StatusMessage = "数据加载完成";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckAndUpgradeDatabase(SQLHelper db)
        {
            // 委托给集中式修复类，确保主界面和配置界面逻辑一致
            BusinessDatabaseFixer.FixSchema(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
        }

        private void SaveData(object obj)
        {
            if (MessageBox.Show("确定要保存当前的布局配置吗？这将覆盖数据库中的现有结构。\n\n注意：如果您手动修改过数据库结构，可能需要备份。", "确认保存", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            if (GlobalData.SysCfg == null) return;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                db.BeginTransaction();
                StatusMessage = "正在保存配置到数据库...";

                try
                {
                    // 删除现有数据 (注意顺序)
                    db.ExecuteNonQuery("DELETE FROM Panel");
                    db.ExecuteNonQuery("DELETE FROM PanelGroup");
                    db.ExecuteNonQuery("DELETE FROM Door");
                    db.ExecuteNonQuery("DELETE FROM DoorGroup");
                    db.ExecuteNonQuery("DELETE FROM Station");

                    // 临时禁用外键约束检查 (如果数据库中有定义外键的话，防止报错)
                    // MySQL: SET FOREIGN_KEY_CHECKS=0;

                    foreach (var stItem in Stations)
                    {
                        // 1. 保存 Station
                        stItem.Entity.Id = 0; 
                        db.Insert(stItem.Entity, "Station");
                        stItem.Entity.Id = Convert.ToInt32(db.ExecuteScalar("SELECT LAST_INSERT_ID()"));

                        // 2. 遍历 Children (门组 和 面板组)
                        foreach (var child in stItem.Children)
                        {
                            if (child is DoorGroupConfigItem dgItem)
                            {
                                dgItem.Entity.Id = 0;
                                db.Insert(dgItem.Entity, "DoorGroup");
                                dgItem.Entity.Id = Convert.ToInt32(db.ExecuteScalar("SELECT LAST_INSERT_ID()"));

                                foreach (var door in dgItem.Children.OfType<DoorConfigItem>())
                                {
                                    door.Entity.Id = 0;
                                    db.Insert(door.Entity, "Door");
                                    door.Entity.Id = Convert.ToInt32(db.ExecuteScalar("SELECT LAST_INSERT_ID()"));
                                }
                            }
                            else if (child is PanelGroupConfigItem pgItem)
                            {
                                pgItem.Entity.Id = 0;
                                db.Insert(pgItem.Entity, "PanelGroup");
                                pgItem.Entity.Id = Convert.ToInt32(db.ExecuteScalar("SELECT LAST_INSERT_ID()"));

                                foreach (var panel in pgItem.Children.OfType<PanelConfigItem>())
                                {
                                    panel.Entity.Id = 0;
                                    db.Insert(panel.Entity, "Panel");
                                    panel.Entity.Id = Convert.ToInt32(db.ExecuteScalar("SELECT LAST_INSERT_ID()"));
                                }
                            }
                        }
                    }

                    // 3. 保存 DoorBitConfig (按类型模板抽取唯一集合保存)
                    // 3. 保存 DoorBitConfig (智能同步，保留所有类型的配置)
                    
                    // 获取当前内存中的所有配置 (基于 DoorBitConfigsByTypeMap)
                    var allMemBits = DoorBitConfigsByTypeMap.Values.SelectMany(x => x).ToList();
                    var allMemKeys = allMemBits.Select(x => x.KeyId).ToHashSet();

                    // 3.1 获取数据库现有的实体 (KeyId -> Id 映射)
                    // 使用 GroupBy 处理 database 层面可能存在的重复 KeyId 风险
                    var existingDbInfo = db.Query<DoorBitConfigEntity>("SELECT Id, KeyId FROM DoorBitConfig")
                                           .GroupBy(x => x.KeyId)
                                           .ToDictionary(g => g.Key, g => g.First().Id);

                    // 3.2 删除: 数据库有但内存中没有的
                    foreach (var key in existingDbInfo.Keys)
                    {
                        if (!allMemKeys.Contains(key))
                        {
                            db.ExecuteNonQuery($"DELETE FROM DoorBitConfig WHERE KeyId = '{key}'");
                        }
                    }

                    // 3.3 更新或新增
                    foreach (var bit in allMemBits)
                    {
                        if (existingDbInfo.TryGetValue(bit.KeyId, out int dbId))
                        {
                             // 关键：确保内存对象的 Id 与数据库一致，否则 Update(WHERE Id=0) 会失败
                             bit.Id = dbId; 
                             db.Update(bit); 
                        }
                        else
                        {
                             bit.Id = 0; // 确保是插入
                             if (string.IsNullOrEmpty(bit.KeyId)) bit.KeyId = Guid.NewGuid().ToString();
                             db.Insert(bit, "DoorBitConfig");
                        }
                    }

                    // 3.4 保存 PanelBitConfig (智能同步)
                    var allPanelMemBits = PanelBitConfigsByPanelMap.Values.SelectMany(x => x).ToList();
                    var allPanelMemKeys = allPanelMemBits.Select(x => x.KeyId).ToHashSet();
                    
                    var existingPanelDbInfo = db.Query<PanelBitConfigEntity>("SELECT Id, KeyId FROM PanelBitConfig")
                                           .GroupBy(x => x.KeyId)
                                           .ToDictionary(g => g.Key, g => g.First().Id);

                    // 删除
                    foreach (var key in existingPanelDbInfo.Keys)
                    {
                        if (!allPanelMemKeys.Contains(key))
                        {
                            db.ExecuteNonQuery($"DELETE FROM PanelBitConfig WHERE KeyId = '{key}'");
                        }
                    }

                    // 更新或新增
                    foreach (var bit in allPanelMemBits)
                    {
                        if (existingPanelDbInfo.TryGetValue(bit.KeyId, out int dbId))
                        {
                             bit.Id = dbId; 
                             db.Update(bit); 
                        }
                        else
                        {
                             bit.Id = 0; 
                             if (string.IsNullOrEmpty(bit.KeyId)) bit.KeyId = Guid.NewGuid().ToString();
                             db.Insert(bit, "PanelBitConfig");
                        }
                    }

                    db.CommitTransaction();
                    StatusMessage = "保存成功！";
                    MessageBox.Show("保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 重新加载以确保所有状态同步
                    LoadData(null);
                }
                catch
                {
                    db.RollbackTransaction();
                    throw;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddStation()
        {
            var newStation = new StationEntity 
            { 
                StationName = "新站台", 
                StationCode = "NEW",
                StationType = StationTypes.FirstOrDefault()?.Id ?? 1,
                KeyId = Guid.NewGuid().ToString()
            };
            var item = new StationConfigItem(newStation);
            Stations.Add(item);
            item.IsSelected = true;
            SelectedItem = item;
        }

        private void AddDoorGroup()
        {
            if (SelectedItem is StationConfigItem stItem)
            {
                var newGroup = new DoorGroupEntity
                {
                    GroupName = stItem.Name + "门组",
                    SortOrder = stItem.Children.Count + 1,
                    KeyId = Guid.NewGuid().ToString(),
                    StationKeyId = stItem.Entity.KeyId // 关联站台 KeyId
                };
                var item = new DoorGroupConfigItem(newGroup, stItem, DoorBitConfigsByTypeMap, this);
                stItem.Children.Add(item);
                stItem.IsExpanded = true;
                item.IsSelected = true;
                SelectedItem = item;
            }
        }

        private void AddPanelGroup()
        {
            if (SelectedItem is StationConfigItem stItem)
            {
                var newGroup = new PanelGroupEntity
                {
                    GroupName = stItem.Name + "面板组",
                    SortOrder = stItem.Children.Count + 1,
                    KeyId = Guid.NewGuid().ToString(),
                    StationKeyId = stItem.Entity.KeyId // 关联站台 KeyId
                };
                var item = new PanelGroupConfigItem(newGroup, stItem);
                stItem.Children.Add(item);
                stItem.IsExpanded = true;
                item.IsSelected = true;
                SelectedItem = item;
            }
        }

        private void AddDoor()
        {
            if (SelectedItem is DoorGroupConfigItem gpItem)
            {
                var newDoor = new DoorEntity
                {
                    DoorName = $"门 {gpItem.Children.Count + 1}",
                    SortOrder = gpItem.Children.Count + 1,
                    ByteLength = 1,
                    KeyId = Guid.NewGuid().ToString(),
                    ParentKeyId = gpItem.Entity.KeyId, // 关联父级 KeyId
                    DoorTypeKeyId = DoorTypes.FirstOrDefault()?.KeyId // 关联默认门类型
                };
                var item = new DoorConfigItem(newDoor, gpItem);
                gpItem.Children.Add(item);
                gpItem.IsExpanded = true;
                item.IsSelected = true;
                SelectedItem = item;
            }
        }

        private void AddPanel()
        {
            if (SelectedItem is PanelGroupConfigItem pgItem)
            {
                var newPanel = new PanelEntity
                {
                    PanelName = $"面板 {pgItem.Children.Count + 1}",
                    SortOrder = pgItem.Children.Count + 1,
                    ByteLength = 1,
                    LayoutRows = 2,
                    LayoutColumns = 3,
                    KeyId = Guid.NewGuid().ToString(),
                    ParentKeyId = pgItem.Entity.KeyId, // 关联父级 KeyId
                    PanelTypeKeyId = PanelTypes.FirstOrDefault()?.KeyId 
                };
                var item = new PanelConfigItem(newPanel, pgItem, PanelBitConfigsByPanelMap, this);
                pgItem.Children.Add(item);
                pgItem.IsExpanded = true;
                item.IsSelected = true;
                SelectedItem = item;
            }
        }

        private void RemoveItem()
        {
            if (SelectedItem == null) return;

            string itemType = "节点";
            string itemName = SelectedItem.Name;

            if (SelectedItem is StationConfigItem) itemType = "站台 (及其下属所有分组和设备)";
            else if (SelectedItem is DoorGroupConfigItem) itemType = "门组 (及其下属所有门单元)";
            else if (SelectedItem is PanelGroupConfigItem) itemType = "面板组 (及其下属所有面板)";
            else if (SelectedItem is DoorConfigItem) itemType = "门单元";
            else if (SelectedItem is PanelConfigItem) itemType = "面板";

            var result = MessageBox.Show($"确定要物理删除{itemType} \"{itemName}\" 吗？\n\n注意：删除后需点击左上角的 [保存配置] 按钮才能正式同步至数据库。", 
                "物理删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (SelectedItem is StationConfigItem stItem)
                {
                    Stations.Remove(stItem);
                    SelectedItem = null;
                }
                else if (SelectedItem is ConfigItemBase childItem)
                {
                    ConfigItemBase parent = null;
                    if (childItem is DoorGroupConfigItem dg) { parent = dg.Parent; dg.Parent.Children.Remove(dg); }
                    else if (childItem is PanelGroupConfigItem pg) { parent = pg.Parent; pg.Parent.Children.Remove(pg); }
                    else if (childItem is DoorConfigItem d) { parent = d.Parent; d.Parent.Children.Remove(d); }
                    else if (childItem is PanelConfigItem p) { parent = p.Parent; p.Parent.Children.Remove(p); }
                    
                    SelectedItem = parent;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDictionaryManager(string type)
        {
            // 这里使用反射或直接 new 窗口，由于我们在同一程序集，可以直接引用
            try
            {
                var win = new DoorMonitorSystem.Views.DictionaryManagerWindow(type, this);
                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
                
                // 窗口关闭后可能需要刷新主界面的一些下拉框绑定，虽然 ObservableCollection 会自动更新
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开管理窗口失败: {ex.Message}");
            }
        }

        #endregion
    }
}
