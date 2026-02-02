using DoorMonitorSystem;
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

        private DevicePointRow _selectedPoint;
        /// <summary>
        /// 当前在 DataGrid 中用户选中的点位配置。
        /// </summary>
        public DevicePointRow SelectedPoint
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
                UpdateNewPointPath(); 
            }
        }

        private void NewPoint_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DevicePointConfigEntity.UiBinding))
            {
                TryResolveBinding(NewPoint.UiBinding);
                UpdateNewPointPath();
            }
        }

        private void TryResolveBinding(string uid)
        {
            if (string.IsNullOrEmpty(uid) || !uid.Contains("_")) return;

            try
            {
                var parts = uid.Split('_');
                if (parts.Length != 2) return;

                string objectKey = parts[0];
                string bitKey = parts[1];

                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();

                // 尝试匹配门
                var door = db.FindAll<DoorEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", objectKey)).FirstOrDefault();
                if (door != null)
                {
                    NewPoint.TargetType = TargetType.Door;
                    NewPoint.TargetKeyId = objectKey;
                    NewPoint.TargetBitConfigKeyId = bitKey;
                    if (string.IsNullOrEmpty(NewPoint.Category) || NewPoint.Category == "Uncategorized")
                        NewPoint.Category = door.DoorName;
                    
                    UpdateNewPointPath(); // 解析成功后刷新路径展示
                    return;
                }

                // 尝试匹配面板
                var panel = db.FindAll<PanelEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", objectKey)).FirstOrDefault();
                if (panel != null)
                {
                    NewPoint.TargetType = TargetType.Panel;
                    NewPoint.TargetKeyId = objectKey;
                    NewPoint.TargetBitConfigKeyId = bitKey;
                    if (string.IsNullOrEmpty(NewPoint.Category) || NewPoint.Category == "Uncategorized")
                        NewPoint.Category = panel.PanelName;
                    
                    UpdateNewPointPath(); // 解析成功后刷新路径展示
                    return;
                }
            }
            catch { }
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

        private ConfigEntity _selectedDevice;
        /// <summary>
        /// 当前正在编辑其点表的目标设备。切换此设备会触发对应点表的重载。
        /// </summary>
        public ConfigEntity SelectedDevice
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
            if (_newPoint != null) _newPoint.PropertyChanged += NewPoint_PropertyChanged;
            LoadLogTypes();
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
        /// 刷新当前 NewPoint 的路径预览。
        /// </summary>
        public void UpdateNewPointPath()
        {
            NewPointBindingPath = GetFullPathForEntity(NewPoint);
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
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return "数据未就绪";
                db.Connect();

                // 快速加载缓存 (由于此方法被频繁调用，建议在 VM 初始化时加载一次，此处为了逻辑清晰先直接查)
                // 后续优化：将字典存为 VM 字段。
                if (entity.TargetType == TargetType.Door)
                {
                    var door = db.FindAll<DoorEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", entity.TargetKeyId)).FirstOrDefault();
                    if (door != null)
                    {
                        var group = db.FindAll<DoorGroupEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", door.ParentKeyId)).FirstOrDefault();
                        var station = group != null ? db.FindAll<StationEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", group.StationKeyId)).FirstOrDefault() : null;
                        var cfg = db.FindAll<DoorBitConfigEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", entity.TargetBitConfigKeyId)).FirstOrDefault();
                        
                        string sName = station?.StationName ?? "未知站台";
                        string gName = group?.GroupName ?? "未知分组";
                        return $"{sName} > {gName} > {door.DoorName} > {cfg?.Description ?? "未知点位"}";
                    }
                }
                else if (entity.TargetType == TargetType.Panel)
                {
                    var panel = db.FindAll<PanelEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", entity.TargetKeyId)).FirstOrDefault();
                    if (panel != null)
                    {
                        var group = db.FindAll<PanelGroupEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", panel.ParentKeyId)).FirstOrDefault();
                        var station = group != null ? db.FindAll<StationEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", group.StationKeyId)).FirstOrDefault() : null;
                        var cfg = db.FindAll<PanelBitConfigEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", entity.TargetBitConfigKeyId)).FirstOrDefault();

                        string sName = station?.StationName ?? "未知站台";
                        string gName = group?.GroupName ?? "未知分组";
                        return $"{sName} > {gName} > {panel.PanelName} > {cfg?.Description ?? "未知点位"}";
                    }
                }
                else if (entity.TargetType == TargetType.Station)
                {
                    var station = db.FindAll<StationEntity>("KeyId = @key", new MySql.Data.MySqlClient.MySqlParameter("@key", entity.TargetKeyId)).FirstOrDefault();
                    if (station != null) return $"{station.StationName} > 站台参数";
                }
            }
            catch { }

            return entity.TargetType == TargetType.None ? "未关联业务对象" : "关联对象已失效或不存在";
        }
    }
}
