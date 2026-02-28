﻿﻿﻿using DoorMonitorSystem;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels; 
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Models.ConfigEntity.Log;
using Base;
using Communicationlib.config;
using ConfigEntity = Communicationlib.config.ConfigEntity;
using DoorMonitorSystem.Models.Ui;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models;
using System.Threading.Tasks;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 点位配置主视图模型
    /// 负责管理资产点位的配置、映射以及数据的生命周期。
    /// 该类采用了 partial 类模式拆分以降低单一文件的复杂度：
    /// - DevvarlistViewModel.Data.cs: 基础数据的加载和增删改查逻辑。
    /// - DevvarlistViewModel.Wizard.cs: 批量生成向导和地址管理功能。
    /// - DevvarlistViewModel.Selector.cs: 提供业务对象树形结构选择和手动绑定逻辑。
    /// - DevvarlistViewModel.Excel.cs: 负责 Excel 报表的导入和导出。
    /// </summary>
    public partial class DevvarlistViewModel : NotifyPropertyChanged
    {
        // --- 缓存优化：一次性加载，避免重复查询数据库 ---
        private Dictionary<string, StationEntity> _stationCache = new();
        private Dictionary<string, DoorGroupEntity> _doorGroupCache = new();
        private Dictionary<string, DoorEntity> _doorCache = new();
        private Dictionary<string, PanelGroupEntity> _panelGroupCache = new();
        private Dictionary<string, PanelEntity> _panelCache = new();
        private Dictionary<string, DoorBitConfigEntity> _doorBitConfigCache = new();
        private Dictionary<string, PanelBitConfigEntity> _panelBitConfigCache = new();
        private Dictionary<string, ParameterDefineEntity> _paramDefineCache = new();
        // -----------------------------------------

        #region 属性定义 (Properties)

        /// <summary>
        /// 当前选定设备下的所有点位配置集合。
        /// 元素类型为 DevicePointRow（UI 展示模型），包含动态计算的业务路径和格式化地址。
        /// </summary>
        public ObservableCollection<DevicePointRow> Points { get; set; } = new ObservableCollection<DevicePointRow>();

        /// <summary>
        /// 系统中所有可用的 PLC/控制器设备列表（用于在 UI 下拉框中切换）。
        /// </summary>
        public ObservableCollection<ConfigEntity> Devices { get; set; } = new ObservableCollection<ConfigEntity>();

        /// <summary>
        /// 日志类型列表，例如“一般记录”、“报警记录”等。
        /// </summary>
        public ObservableCollection<LogTypeEntity> LogTypes { get; set; } = new ObservableCollection<LogTypeEntity>();

        private DevicePointRow? _selectedPoint;
        /// <summary>
        /// 当前在 DataGrid 中用户选中的点位配置。
        /// </summary>
        public DevicePointRow? SelectedPoint
        {
            get => _selectedPoint;
            set { _selectedPoint = value; OnPropertyChanged(); }
        }

        private DevicePointConfigEntity _newPoint = new DevicePointConfigEntity() { DataType = "Word" };
        /// <summary>
        /// 正在进行新增或编辑的点位对象，直接绑定到 UI 的表单输入框。
        /// </summary>
        public DevicePointConfigEntity NewPoint
        {
            get => _newPoint;
            set 
            { 
                if (_newPoint != null) _newPoint.PropertyChanged -= NewPoint_PropertyChanged;
                _newPoint = value; 
                if (_newPoint != null) _newPoint.PropertyChanged += NewPoint_PropertyChanged;
                OnPropertyChanged();
                // 异步更新路径，避免阻塞 UI
                _ = UpdateNewPointPathAsync(); 
            }
        }

        private void NewPoint_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DevicePointConfigEntity.UiBinding))
            {
                // 异步解析绑定并更新路径
                _ = ResolveBindingAndPathAsync(NewPoint.UiBinding);
            }
        }

        private async Task ResolveBindingAndPathAsync(string uid)
        {
            await TryResolveBindingAsync(uid);
            await UpdateNewPointPathAsync();
        }

        private async Task TryResolveBindingAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid) || !uid.Contains("_")) return;
            LogHelper.Info($"[DevvarlistViewModel] 开始解析绑定 UID: {uid}");

            await Task.Run(() =>
            {
                // 优化：不再连接数据库，直接从内存缓存中查找，极大提升响应速度。
                try 
                {
                    var parts = uid.Split('_');
                    if (parts.Length != 2) return;

                    string objectKey = parts[0];
                    string bitKey = parts[1];
                    
                    if (_doorCache.TryGetValue(objectKey, out var door))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NewPoint.TargetType = TargetType.Door;
                            NewPoint.TargetKeyId = objectKey;
                            NewPoint.TargetBitConfigKeyId = bitKey;
                            if (string.IsNullOrEmpty(NewPoint.Category) || NewPoint.Category == "Uncategorized")
                                NewPoint.Category = door?.DoorName;
                        });
                        return;
                    }

                    if (_panelCache.TryGetValue(objectKey, out var panel))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            NewPoint.TargetType = TargetType.Panel;
                            NewPoint.TargetKeyId = objectKey;
                            NewPoint.TargetBitConfigKeyId = bitKey;
                            if (string.IsNullOrEmpty(NewPoint.Category) || NewPoint.Category == "Uncategorized")
                                NewPoint.Category = panel?.PanelName;
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"[DevvarlistViewModel] 异步解析绑定UID '{uid}' 失败", ex);
                }
            });
        }

        private string _newPointBindingPath = "未绑定";
        /// <summary>
        /// 当前待编辑点位的业务全路径预览（如：1号站台 > 下行 > 101门 > 开门状态）。
        /// 用于在编辑弹窗中悬停显示。
        /// </summary>
        public string NewPointBindingPath
        {
            get => _newPointBindingPath;
            set { _newPointBindingPath = value; OnPropertyChanged(); }
        }

        private ConfigEntity? _selectedDevice;
        /// <summary>
        /// 当前正在编辑其点表的目标设备。切换此设备会触发对应点表的重载。
        /// </summary>
        public ConfigEntity? SelectedDevice
        {
            get => _selectedDevice;
            set 
            { 
                _selectedDevice = value; 
                OnPropertyChanged();
                // 切换设备后，将待编辑点位的归属设备 ID 同步更新。
                if(_selectedDevice != null) NewPoint.SourceDeviceId = _selectedDevice.ID;
                LoadPoints(); 
            }
        }

        private bool _isBatchPopupVisible;
        /// <summary>
        /// 控制批量生成弹窗的显示。
        /// </summary>
        public bool IsBatchPopupVisible
        {
            get => _isBatchPopupVisible;
            set { _isBatchPopupVisible = value; OnPropertyChanged(); }
        }

        private string _batchStartAddress = "0";
        public string BatchStartAddress
        {
            get => _batchStartAddress;
            set { _batchStartAddress = value; OnPropertyChanged(); }
        }

        private int _batchStride = 1;
        public int BatchStride
        {
            get => _batchStride;
            set { _batchStride = value; OnPropertyChanged(); }
        }

        private string? _selectedBatchTargetType;
        public string? SelectedBatchTargetType
        {
            get => _selectedBatchTargetType;
            set { _selectedBatchTargetType = value; OnPropertyChanged(); LoadBatchGroups(); }
        }

        public List<string> BatchTargetTypes { get; set; } = new List<string> { "门点位", "面板点位", "站台参数" };

        private StationEntity? _selectedBatchStation;
        public StationEntity? SelectedBatchStation
        {
            get => _selectedBatchStation;
            set { _selectedBatchStation = value; OnPropertyChanged(); LoadBatchGroups(); }
        }

        public ObservableCollection<StationEntity> BatchStations { get; set; } = new ObservableCollection<StationEntity>();

        private DoorMonitorSystem.Models.Ui.BatchGroupItem? _selectedBatchGroup;
        /// <summary>
        /// 当前选中的批量生成目标组（如：上行门组、1号值班室面板组）。
        /// </summary>
        public DoorMonitorSystem.Models.Ui.BatchGroupItem? SelectedBatchGroup
        {
            get => _selectedBatchGroup;
            set { _selectedBatchGroup = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 批量生成的逻辑分组列表。
        /// </summary>
        public ObservableCollection<DoorMonitorSystem.Models.Ui.BatchGroupItem> BatchGroups { get; set; } = new ObservableCollection<DoorMonitorSystem.Models.Ui.BatchGroupItem>();

        private bool _isEditing;
        /// <summary>
        /// 标识当前界面是否处于“编辑已有记录”模式。
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActionName)); }
        }

        private bool _isEditorVisible;
        /// <summary>
        /// 控制编辑对话框（或侧边栏）的显示与隐藏。
        /// </summary>
        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            set { _isEditorVisible = value; OnPropertyChanged(); }
        }

        private bool _isPopupOpen;
        /// <summary>
        /// 控制对象树弹窗的显示。
        /// </summary>
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set 
            { 
                _isPopupOpen = value; 
                OnPropertyChanged();
                // 当弹窗打开时，执行数据加载
                if (_isPopupOpen && SelectorTree.Count == 0) LoadSelectorNodes();
            }
        }

        /// <summary>
        /// 根据当前的编辑状态，动态返回按钮的文字。
        /// </summary>
        public string ActionName => IsEditing ? "保存修改" : "添加点位";
        #endregion

        #region 命令定义 (Commands)

        /// <summary>
        /// 保存当前编辑的点位（新增或修改）。
        /// </summary>
        public ICommand AddPointCommand => new RelayCommand(AddPoint);
        public ICommand LoadPointsCommand => new RelayCommand(obj => LoadPoints());

        /// <summary>
        /// 开始编辑选中的点位，将数据载入编辑表单。
        /// </summary>
        public ICommand StartEditCommand => new RelayCommand(StartEdit);

        /// <summary>
        /// 取消当前的编辑操作并关闭表单。
        /// </summary>
        public ICommand CancelEditCommand => new RelayCommand(CancelEdit);

        /// <summary>
        /// 删除当前选中的点位配置。
        /// </summary>
        public ICommand DeletePointCommand => new RelayCommand(DeletePoint);

        /// <summary>
        /// 打开新增点位的空白表单。
        /// </summary>
        public ICommand OpenAddFormCommand => new RelayCommand(OpenAddForm);
        
        /// <summary>
        /// 导出当前点表。
        /// </summary>
        public ICommand ExportCommand => ExportPointsCommand;
        public ICommand ExportPointsCommand => new RelayCommand(obj => ExportPoints());

        /// <summary>
        /// 从文件导入点表。
        /// </summary>
        public ICommand ImportCommand => ImportPointsCommand;
        public ICommand ImportPointsCommand => new RelayCommand(obj => ImportPoints());

        /// <summary>
        /// 打开批量生成向导。
        /// </summary>
        public ICommand OpenBatchPopupCommand => new RelayCommand(OpenBatchPopup);

        /// <summary>
        /// 关闭批量生成向导。
        /// </summary>
        public ICommand CloseBatchPopupCommand => new RelayCommand(obj => IsBatchPopupVisible = false);

        /// <summary>
        /// 确认批量生成。
        /// </summary>
        public ICommand ConfirmBatchGenerateCommand => new RelayCommand(GenerateBatchPoints);

        /// <summary>
        /// 清除绑定逻辑。
        /// </summary>
        public ICommand ClearBindingCommand => new RelayCommand(obj => {
            if (NewPoint != null) {
                NewPoint.UiBinding = null;
                NewPoint.TargetKeyId = null;
                NewPoint.TargetBitConfigKeyId = null;
                NewPoint.BindingRole = null;
                OnPropertyChanged(nameof(NewPoint));
            }
        });

        /// <summary>
        /// 地址映射包管理 (预留或简单实现)。
        /// </summary>
        public ICommand OpenAddressMgrCommand => new RelayCommand(obj => MessageBox.Show("地址映射包管理功能开发中..."));
        #endregion

        /// <summary>
        /// 构造函数，初始化基础数据加载。
        /// </summary>
        public DevvarlistViewModel()
        {
            LogHelper.Info("[DevvarlistViewModel] 构造函数开始执行");
            if (_newPoint != null) _newPoint.PropertyChanged += NewPoint_PropertyChanged;
            LoadLogTypes();
            LoadCaches(); // 关键优化：加载所有缓存
            LoadDevices();
            LoadPoints(); 
        }

        /// <summary>
        /// 从数据库初始化日志分类数据。
        /// </summary>
        private void LoadLogTypes()
        {
            LogTypes.Clear();
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();
                
                db.CreateTableFromModel<LogTypeEntity>(); 
                var types = db.FindAll<LogTypeEntity>();
                var sortedTypes = types.OrderBy(t => t.SortOrder).ToList();

                foreach (var t in sortedTypes) LogTypes.Add(t);

                // 同步更新全局日志类型映射，供 UI 显示使用
                PointLogEntity.LogTypeMap.Clear();
                foreach (var t in sortedTypes) PointLogEntity.LogTypeMap[t.Id] = t.Name;
                
                // 如果库中无数据，则初始化默认的“一般”和“报警”分类。
                if (LogTypes.Count == 0)
                {
                    var defaults = new List<LogTypeEntity>
                    {
                        new LogTypeEntity { Name = "一般记录", SortOrder = 1 },
                        new LogTypeEntity { Name = "报警记录", SortOrder = 2 }
                    };
                    foreach (var d in defaults) db.Insert(d);
                    
                    types = db.FindAll<LogTypeEntity>();
                    foreach (var t in types.OrderBy(x => x.SortOrder))
                    {
                        LogTypes.Add(t);
                        PointLogEntity.LogTypeMap[t.Id] = t.Name;
                    }
                }
            }
            catch (Exception ex) { LogHelper.Error("加载日志分类失败", ex); }
        }

        /// <summary>
        /// 将所有用于路径解析的实体一次性加载到内存缓存中，极大提升性能。
        /// </summary>
        private void LoadCaches()
        {
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();

                _stationCache = db.FindAll<StationEntity>().ToDictionary(e => e.KeyId, e => e);
                _doorGroupCache = db.FindAll<DoorGroupEntity>().ToDictionary(e => e.KeyId, e => e);
                _doorCache = db.FindAll<DoorEntity>().ToDictionary(e => e.KeyId, e => e);
                _panelGroupCache = db.FindAll<PanelGroupEntity>().ToDictionary(e => e.KeyId, e => e);
                _panelCache = db.FindAll<PanelEntity>().ToDictionary(e => e.KeyId, e => e);
                _doorBitConfigCache = db.FindAll<DoorBitConfigEntity>().ToDictionary(e => e.KeyId, e => e);
                _panelBitConfigCache = db.FindAll<PanelBitConfigEntity>().ToDictionary(e => e.KeyId, e => e);
                _paramDefineCache = db.FindAll<ParameterDefineEntity>().ToDictionary(e => e.BindingKey, e => e);
                LogHelper.Info("[DevvarlistViewModel] 缓存加载完成");
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载实体缓存失败，路径解析功能可能受影响。", ex);
            }
        }

        /// <summary>
        /// 刷新当前 NewPoint 的路径预览。
        /// </summary>
        public void UpdateNewPointPath()
        {
            _ = UpdateNewPointPathAsync();
        }

        public async Task UpdateNewPointPathAsync()
        {
            NewPointBindingPath = "查询中...";
            string path = await Task.Run(() => GetFullPathForEntity(NewPoint));
            NewPointBindingPath = path;
        }

        /// <summary>
        /// 根据点位配置中的 Target 信息，解析出由中文名称组成的业务全路径。
        /// </summary>
        /// <param name="entity">点位配置实体</param>
        /// <returns>全路径字符串 (如: 站台 > 门组 > 门 > 功能)</returns>
        private string GetFullPathForEntity(DevicePointConfigEntity entity)
        {
            if (entity == null) return "未绑定";
            
            try 
            {
                // 优化：直接从内存缓存中查找，替代频繁的数据库查询
                if (entity.TargetType == TargetType.Door)
                {
                    if (_doorCache.TryGetValue(entity.TargetKeyId ?? "", out var door) &&
                        _doorGroupCache.TryGetValue(door.ParentKeyId ?? "", out var group) &&
                        _stationCache.TryGetValue(group.StationKeyId ?? "", out var station) &&
                        _doorBitConfigCache.TryGetValue(entity.TargetBitConfigKeyId ?? "", out var cfg))
                    {
                        return $"{station.StationName} > {group.GroupName} > {door.DoorName} > {cfg.Description}";
                    }
                }
                else if (entity.TargetType == TargetType.Panel)
                {
                    if (_panelCache.TryGetValue(entity.TargetKeyId ?? "", out var panel) &&
                        _panelGroupCache.TryGetValue(panel.ParentKeyId ?? "", out var group) &&
                        _stationCache.TryGetValue(group.StationKeyId ?? "", out var station) &&
                        _panelBitConfigCache.TryGetValue(entity.TargetBitConfigKeyId ?? "", out var cfg))
                    {
                        return $"{station.StationName} > {group.GroupName} > {panel.PanelName} > {cfg.Description}";
                    }
                }
                else if (entity.TargetType == TargetType.Station)
                {
                    if (_stationCache.TryGetValue(entity.TargetKeyId ?? "", out var station))
                    {
                        string fullPath = $"{station.StationName} > 站台参数";
                        if (!string.IsNullOrEmpty(entity.UiBinding) && _paramDefineCache.TryGetValue(entity.UiBinding, out var scfg))
                        {
                            string roleName = entity.BindingRole switch
                            {
                                "Read" => "读取",
                                "Write" => "写入",
                                "Auth" => "鉴权",
                                "AuthRow" => "行授权",
                                _ => entity.BindingRole
                            };
                            string roleSuffix = string.IsNullOrEmpty(roleName) ? "" : $" ({roleName})";
                            fullPath += $" > {scfg.Label}{roleSuffix}";
                        }
                        return fullPath;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"路径解析失败 (实体ID: {entity.Id})", ex);
            }

            return entity.TargetType == TargetType.None ? "未关联业务对象" : "关联对象已失效或不存在";
        }
        public List<string> PointDataTypes => Enum.GetNames(typeof(PointDataType)).ToList();
    }
}
