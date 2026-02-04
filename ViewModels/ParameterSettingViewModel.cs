using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Base;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Services;
using System.Windows.Input;
using DoorMonitorSystem.Models.ConfigEntity;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 参数设置页面的主视图模型
    /// 作为一个容器 VM，它动态监听系统站台配置，并从数据库加载底层 ASD 映射规则。
    /// </summary>
    public class ParameterSettingViewModel : NotifyPropertyChanged
    {
        #region 字段与属性

        private ObservableCollection<StationParameterViewModel> _stationSettings;
        private readonly ObservableCollection<AsdModelMapping> _asdModels;

        /// <summary>
        /// 所有站台对应的参数配置单元集合
        /// </summary>
        public ObservableCollection<StationParameterViewModel> StationSettings
        {
            get => _stationSettings;
            set { _stationSettings = value; OnPropertyChanged(); }
        }

        private bool _isManagementMode;
        /// <summary>是否处于模板管理模式</summary>
        public bool IsManagementMode
        {
            get => _isManagementMode;
            set { _isManagementMode = value; OnPropertyChanged(); }
        }

        /// <summary>管理用的参数定义集合</summary>
        public ObservableCollection<ParameterDefineEntity> ManageParameterDefines { get; set; } = new ObservableCollection<ParameterDefineEntity>();
        
        /// <summary>管理用的 ASD 模型映射集合</summary>
        public ObservableCollection<AsdModelMappingEntity> ManageAsdModels { get; set; } = new ObservableCollection<AsdModelMappingEntity>();

        /// <summary>
        /// 管理员可见性 (用于控制模板管理入口)
        /// </summary>
        public System.Windows.Visibility AdminVisibility => 
            (GlobalData.CurrentUser?.Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true) 
            ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        /// <summary>
        /// 刷新权限状态 (当用户切换时调用)
        /// </summary>
        public void UpdatePermissions()
        {
            OnPropertyChanged(nameof(AdminVisibility));
            foreach (var st in StationSettings)
            {
                st.UpdatePermissions();
            }
        }

        #endregion

        #region 命令 (Commands)

        public ICommand ToggleManagementCommand => new RelayCommand(obj => {
            IsManagementMode = !IsManagementMode;
            if (IsManagementMode) LoadManagementData();
        });

        public ICommand AddDefCommand => new RelayCommand(obj => {
            ManageParameterDefines.Add(new ParameterDefineEntity { Label = "新参数", DataType = "Int16", SortOrder = ManageParameterDefines.Count + 1 });
        });

        public ICommand DeleteDefCommand => new RelayCommand(obj => {
            if (obj is ParameterDefineEntity entity) {
                if (entity.Id > 0) DataManager.Instance.DeleteParameterDefine(entity);
                ManageParameterDefines.Remove(entity);
            }
        });

        public ICommand SaveDefsCommand => new RelayCommand(obj => {
            foreach (var item in ManageParameterDefines) DataManager.Instance.SaveParameterDefine(item);
            System.Windows.MessageBox.Show("参数项定义已保存到数据库。同步至实时界面中...", "操作成功");
            ReloadRuntimeParameters();
        });

        public ICommand AddModelCommand => new RelayCommand(obj => {
            ManageAsdModels.Add(new AsdModelMappingEntity { DisplayName = "NEW_MODEL", PlcId = 1 });
        });

        public ICommand DeleteModelCommand => new RelayCommand(obj => {
            if (obj is AsdModelMappingEntity entity) {
                if (entity.Id > 0) DataManager.Instance.DeleteAsdModel(entity);
                ManageAsdModels.Remove(entity);
            }
        });

        public ICommand SaveModelsCommand => new RelayCommand(obj => {
            foreach (var item in ManageAsdModels) DataManager.Instance.SaveAsdModel(item);
            System.Windows.MessageBox.Show("站台模型配置已保存。重新加载下拉框...", "操作成功");
            ReloadRuntimeModels();
        });

        #endregion

        private void LoadManagementData()
        {
            ManageParameterDefines.Clear();
            var defs = DataManager.Instance.LoadParameterDefinesFromDb().OrderBy(p => p.SortOrder).ToList();
            foreach (var item in defs) ManageParameterDefines.Add(item);

            ManageAsdModels.Clear();
            var models = DataManager.Instance.LoadAsdModelsFromDb().OrderBy(m => m.PlcId).ToList();
            foreach (var item in models) ManageAsdModels.Add(item);
        }

        private void ReloadRuntimeParameters()
        {
            foreach (var st in StationSettings)
            {
                st.LoadParameterListFromDb(); // 重新从 DB 加载定义
                st.BindConfigToParameters();  // 重新绑定点位
            }
        }

        private void ReloadRuntimeModels()
        {
            var modelEntities = DataManager.Instance.LoadAsdModelsFromDb();
            _asdModels.Clear();
            foreach (var e in modelEntities)
            {
                _asdModels.Add(new AsdModelMapping { DisplayName = e.DisplayName, PlcId = e.PlcId });
            }

            foreach (var st in StationSettings)
            {
                // StationParameterViewModel 使用的是 _asdModels 引用，所以 Clear/Add 会自动同步
                // 但为了保险，可以显式清理一下选中项如果它被删除了
                if (st.SelectedAsdModel != null && !_asdModels.Contains(st.SelectedAsdModel))
                {
                    st.SelectedAsdModel = _asdModels.FirstOrDefault();
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ParameterSettingViewModel()
        {
            StationSettings = new ObservableCollection<StationParameterViewModel>();
            
            // 1. 从数据库读取 ASD 型号映射规则，实现动态扩展
            var modelEntities = DataManager.Instance.LoadAsdModelsFromDb();
            _asdModels = new ObservableCollection<AsdModelMapping>(
                modelEntities.OrderBy(e => e.PlcId)
                .Select(e => new AsdModelMapping { DisplayName = e.DisplayName, PlcId = e.PlcId })
            );

            // 如果数据库为空（理论上 DataManager 会初始化，但此处做防御），提供基础 fallback
            if (_asdModels.Count == 0)
            {
                _asdModels.Add(new AsdModelMapping { DisplayName = "ASD_DEFAULT", PlcId = 1 });
            }

            // 2. 初始同步：尝试从 MainViewModel 抓取当前已有的站台
            SyncStations();

            // 3. 动态监听全局站台列表变更
            if (GlobalData.MainVm?.Stations != null)
            {
                GlobalData.MainVm.Stations.CollectionChanged += OnGlobalStationsChanged;
            }
        }

        /// <summary>
        /// 监听全局站台列表的变化
        /// </summary>
        private void OnGlobalStationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SyncStations();
        }

        /// <summary>
        /// 站台列表精准同步逻辑
        /// </summary>
        private void SyncStations()
        {
            if (GlobalData.MainVm?.Stations == null) return;

            var currentStations = GlobalData.MainVm.Stations
                                       .Where(s => s.Station != null)
                                       .ToList();

            // 更新或添加新站台
            foreach (var stVm in currentStations)
            {
                var name = stVm.Station.StationName;
                int stationId = stVm.Station.StationId;

                // 尝试解析该站台关联的实际物理设备ID
                int? devId = ResolveDeviceId(name);

                var existing = StationSettings.FirstOrDefault(x => x.StationName == name);
                if (existing == null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        StationSettings.Add(new StationParameterViewModel(name, _asdModels, devId, stationId, stVm.Station.KeyId));
                    });
                }
                else
                {
                    // 同步 ID (解决异步加载或配置变更)
                    existing.TargetDeviceId = devId;
                    existing.StationId = stationId;
                    existing.StationKeyId = stVm.Station.KeyId;
                }
            }

            // 移除已删除站台
            var toRemove = StationSettings.Where(s => !currentStations.Any(x => x.Station.StationName == s.StationName)).ToList();
            foreach (var item in toRemove)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    StationSettings.Remove(item);
                });
            }

            // 防控与兜底逻辑
            if (StationSettings.Count == 0 && currentStations.Count == 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    if (!StationSettings.Any(x => x.StationName == "通用 ASD 节点"))
                    {
                        StationSettings.Add(new StationParameterViewModel("通用 ASD 节点", _asdModels));
                    }
                });
            }
            else if (StationSettings.Count > 0 && currentStations.Count > 0)
            {
                var dummy = StationSettings.FirstOrDefault(x => x.StationName == "通用 ASD 节点");
                if (dummy != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => StationSettings.Remove(dummy));
                }
            }
        }

        /// <summary>
        /// 智能解析站台对应的 DeviceID
        /// </summary>
        private int? ResolveDeviceId(string stationName)
        {
            try
            {
                // 根据站台名称反查绑定的 ViewModel 实例
                var stVm = GlobalData.MainVm?.Stations.FirstOrDefault(s => s.Station?.StationName == stationName);
                if (stVm == null) return null;

                // 策略 1：尝试从该站台下的“门/屏蔽门”配置中提取关联的设备ID。
                // 这适用于那些已经配置了门状态监控的站台。
                var firstDoor = stVm.Station.DoorGroups.FirstOrDefault()?.Doors.FirstOrDefault();
                if (firstDoor != null && firstDoor.Bits.Count > 0)
                {
                    int bitId = firstDoor.Bits[0].BitId;
                    var cfg = DeviceCommunicationService.Instance?.GetPointConfigById(bitId);
                    if (cfg?.SourceDeviceId > 0) return cfg.SourceDeviceId;
                }

                // 策略 2：回退逻辑。如果该站台还没配门（例如仅作为参数集），
                // 则尝试提取该站台 ID 下已配置的任意点位的 DeviceId。
                var stationKeyId = stVm.Station.KeyId;
                var stationPoints = DeviceCommunicationService.Instance?.GetPointConfigsForStation(stationKeyId);
                var firstPoint = stationPoints?.FirstOrDefault();
                if (firstPoint != null) return firstPoint.SourceDeviceId;
            }
            catch(Exception ex) 
            {
                Assets.Helper.LogHelper.Warn($"[ParamSetting] 设备 ID 解析异常 ({stationName}): {ex.Message}");
            }
            return null;
        }
        public List<string> PointDataTypes => Enum.GetNames(typeof(PointDataType)).ToList();
    }
}
